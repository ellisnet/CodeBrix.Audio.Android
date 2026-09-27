using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Audio.Android.Internal;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.Content.Res;

namespace CodeBrix.Audio.Android;

/// <summary>Copies files and folders packaged as Android assets out of the APK into the
/// application's private storage, once per installed build, and returns their paths.</summary>
/// <remarks>
/// <para>Sample libraries - a SoundFont, an SFZ or Decent Sampler folder - are delivered to a
/// desktop application as files beside the executable, and the CodeBrix.Audio instrument
/// libraries open them by path. An Android application has no such folder: the asset is inside
/// the APK, readable only as a stream. This class bridges the two. Ask it for an asset by its
/// path under the project's assets, and it extracts the asset to
/// <see cref="RootDirectory"/> the first time, reuses the copy on every later launch, and
/// re-extracts after the application is updated. Hand the returned path to the instrument
/// library (for example <c>FluidR3GmInstrumentLibrary.UseSoundFontAt(path)</c>).</para>
/// <para>Extraction is file IO on a potentially large file: call
/// <see cref="Materialize(string, Context)"/> from a background thread, or use
/// <see cref="MaterializeAsync(string, IProgress{long}, CancellationToken, Context)"/>. A copy
/// interrupted by a crash or cancellation is never mistaken for a complete one: the asset is
/// written under a temporary name and renamed into place only when finished.</para>
/// <para>Every member takes an optional <see cref="Context"/>. When it is omitted the
/// application context retained by <see cref="CodeBrixAndroidAudio.Initialize"/> is used, so an
/// application that has already initialised audio passes nothing.</para>
/// </remarks>
public static class AndroidPackagedAssets
{
    private const string RootFolderName = "codebrix-audio-assets";
    private const int CopyBufferSize = 1 << 16;
    private static readonly object Gate = new();
    private static Context _context;

    internal static void Attach(Context applicationContext)
    {
        lock (Gate) _context = applicationContext;
    }

    /// <summary>The folder under the application's private files directory that extracted
    /// assets live in.</summary>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    /// <returns>The absolute path of the folder, which may not exist yet.</returns>
    public static string RootDirectory(Context context = null)
        => Path.Combine(Resolve(context).FilesDir.AbsolutePath, RootFolderName);

    /// <summary>Extracts an asset if it has not already been extracted for this installed
    /// build, and returns the path of the copy.</summary>
    /// <param name="assetPath">The asset's path under the project's assets, using forward slashes -
    /// a single file such as <c>FluidR3_GM.sf2</c>, or a folder, which is extracted with everything
    /// under it.</param>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    /// <returns>The absolute path of the extracted file or folder.</returns>
    /// <exception cref="ArgumentException">The asset path is empty, absolute, or escapes the assets folder.</exception>
    /// <exception cref="FileNotFoundException">No such asset is packaged in the application.</exception>
    /// <exception cref="InvalidOperationException">No context was given and <see cref="CodeBrixAndroidAudio.Initialize"/> has not run.</exception>
    /// <remarks>Blocking file IO; call it from a background thread.</remarks>
    public static string Materialize(string assetPath, Context context = null)
        => Materialize(Resolve(context), AssetStamp.NormalizeAssetPath(assetPath), null, CancellationToken.None);

    /// <summary>The asynchronous form of <see cref="Materialize(string, Context)"/>.</summary>
    /// <param name="assetPath">The asset's path under the project's assets.</param>
    /// <param name="progress">Receives the number of bytes copied so far. The total is not known in
    /// advance for a compressed asset, so this is a running count, not a fraction.</param>
    /// <param name="cancellationToken">Cancels the copy; a partial copy is discarded.</param>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    /// <returns>The absolute path of the extracted file or folder.</returns>
    public static Task<string> MaterializeAsync(string assetPath, IProgress<long> progress = null,
        CancellationToken cancellationToken = default, Context context = null)
    {
        var resolved = Resolve(context);
        var normalized = AssetStamp.NormalizeAssetPath(assetPath);
        return Task.Run(() => Materialize(resolved, normalized, progress, cancellationToken), cancellationToken);
    }

    /// <summary>Whether a complete, current copy of the asset already exists.</summary>
    /// <param name="assetPath">The asset's path under the project's assets.</param>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    /// <returns>True when <see cref="Materialize(string, Context)"/> would return without copying.</returns>
    public static bool IsMaterialized(string assetPath, Context context = null)
    {
        var resolved = Resolve(context);
        var normalized = AssetStamp.NormalizeAssetPath(assetPath);
        var destination = AssetStamp.DestinationFor(RootDirectory(resolved), normalized);
        return IsCurrent(destination, ExpectedStamp(resolved, normalized));
    }

    /// <summary>Deletes the extracted copy of an asset, if any. The next
    /// <see cref="Materialize(string, Context)"/> extracts it again.</summary>
    /// <param name="assetPath">The asset's path under the project's assets.</param>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    public static void Remove(string assetPath, Context context = null)
    {
        var resolved = Resolve(context);
        var destination = AssetStamp.DestinationFor(RootDirectory(resolved), AssetStamp.NormalizeAssetPath(assetPath));
        lock (Gate) DeleteCopy(destination);
    }

    /// <summary>Deletes every extracted asset.</summary>
    /// <param name="context">Any context; omit it after <see cref="CodeBrixAndroidAudio.Initialize"/>.</param>
    public static void RemoveAll(Context context = null)
    {
        var root = RootDirectory(Resolve(context));
        lock (Gate) { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static string Materialize(Context context, string normalized, IProgress<long> progress, CancellationToken cancellationToken)
    {
        var destination = AssetStamp.DestinationFor(RootDirectory(context), normalized);
        var stamp = ExpectedStamp(context, normalized);
        lock (Gate)
        {
            if (IsCurrent(destination, stamp)) return destination;
            DeleteCopy(destination);
            var partial = AssetStamp.PartialPathFor(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var assets = context.Assets ?? throw new InvalidOperationException("The application's asset manager is unavailable.");
            long copied = 0;
            try
            {
                if (IsAssetDirectory(assets, normalized))
                {
                    Directory.CreateDirectory(partial);
                    CopyDirectory(assets, normalized, partial, progress, ref copied, cancellationToken);
                    Directory.Move(partial, destination);
                }
                else
                {
                    CopyFile(assets, normalized, partial, progress, ref copied, cancellationToken);
                    File.Move(partial, destination, true);
                }
            }
            catch
            {
                DeletePartial(partial);
                throw;
            }
            File.WriteAllText(AssetStamp.StampPathFor(destination), stamp);
            return destination;
        }
    }

    private static string ExpectedStamp(Context context, string normalized)
    {
        var manager = context.PackageManager ?? throw new InvalidOperationException("The application's package manager is unavailable.");
        var info = manager.GetPackageInfo(context.PackageName, PackageManager.PackageInfoFlags.Of(0L))
            ?? throw new InvalidOperationException("The application's package information is unavailable.");
        return AssetStamp.Compose(info.LongVersionCode, info.LastUpdateTime, normalized);
    }

    private static bool IsCurrent(string destination, string expectedStamp)
    {
        if (!File.Exists(destination) && !Directory.Exists(destination)) return false;
        var stampPath = AssetStamp.StampPathFor(destination);
        return File.Exists(stampPath) && AssetStamp.Matches(File.ReadAllText(stampPath), expectedStamp);
    }

    private static bool IsAssetDirectory(AssetManager assets, string assetPath)
    {
        var entries = assets.List(assetPath);
        if (entries != null && entries.Length > 0) return true;
        // An empty listing is a file, an empty folder, or nothing at all; opening tells them apart.
        try { using var probe = assets.Open(assetPath); return false; }
        catch (Java.IO.FileNotFoundException ex)
        {
            throw new FileNotFoundException($"The application packages no asset at '{assetPath}'. Add it to the project as an AndroidAsset item (a package that ships the file does this through its build targets), and check the path and case.", assetPath, ex);
        }
    }

    private static void CopyDirectory(AssetManager assets, string assetPath, string target, IProgress<long> progress, ref long copied, CancellationToken cancellationToken)
    {
        foreach (var entry in assets.List(assetPath) ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var child = assetPath + "/" + entry;
            var childTarget = Path.Combine(target, entry);
            var grandchildren = assets.List(child);
            if (grandchildren != null && grandchildren.Length > 0)
            {
                Directory.CreateDirectory(childTarget);
                CopyDirectory(assets, child, childTarget, progress, ref copied, cancellationToken);
            }
            else
            {
                CopyFile(assets, child, childTarget, progress, ref copied, cancellationToken);
            }
        }
    }

    private static void CopyFile(AssetManager assets, string assetPath, string target, IProgress<long> progress, ref long copied, CancellationToken cancellationToken)
    {
        using var source = assets.Open(assetPath);
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);
        var buffer = new byte[CopyBufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            copied += read;
            progress?.Report(copied);
        }
    }

    private static void DeleteCopy(string destination)
    {
        var stampPath = AssetStamp.StampPathFor(destination);
        if (File.Exists(stampPath)) File.Delete(stampPath);
        if (File.Exists(destination)) File.Delete(destination);
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        DeletePartial(AssetStamp.PartialPathFor(destination));
    }

    private static void DeletePartial(string partial)
    {
        if (File.Exists(partial)) File.Delete(partial);
        if (Directory.Exists(partial)) Directory.Delete(partial, true);
    }

    private static Context Resolve(Context context)
    {
        if (context != null)
        {
            return context.ApplicationContext ?? throw new ArgumentException("An application context is required.", nameof(context));
        }
        lock (Gate)
        {
            return _context ?? throw new InvalidOperationException("Pass a Context, or call CodeBrixAndroidAudio.Initialize(context) first so AndroidPackagedAssets knows the application.");
        }
    }
}

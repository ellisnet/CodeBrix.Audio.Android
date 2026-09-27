using System;
using System.IO;

namespace CodeBrix.Audio.Android.Internal;

// The platform-independent half of AndroidPackagedAssets: asset-path validation, the on-disk
// layout of an extracted copy, and the stamp that says which installed application version an
// extracted copy belongs to. Kept free of Android types so the host tests can compile it in.
internal static class AssetStamp
{
    internal const string StampSuffix = ".codebrix-stamp";
    internal const string PartialSuffix = ".codebrix-partial";
    private const string Version = "1";

    // An asset path is a relative, forward-slash path inside the APK's assets/ folder. Anything
    // that could escape the extraction root, or that Android's asset manager could not name, is
    // rejected here rather than discovered as a missing file later.
    internal static string NormalizeAssetPath(string assetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        var normalized = assetPath.Replace('\\', '/').Trim().Trim('/');
        if (normalized.Length == 0) throw new ArgumentException("An asset path must name a file or folder inside the application's assets.", nameof(assetPath));
        if (normalized.Contains("//", StringComparison.Ordinal)) throw new ArgumentException("An asset path must not contain empty segments.", nameof(assetPath));
        if (normalized.Contains(':', StringComparison.Ordinal)) throw new ArgumentException("An asset path must be relative to the application's assets; it cannot carry a drive or scheme.", nameof(assetPath));
        foreach (var segment in normalized.Split('/'))
        {
            if (segment == "." || segment == "..") throw new ArgumentException("An asset path must not contain '.' or '..' segments.", nameof(assetPath));
        }
        return normalized;
    }

    // Extracted copies mirror the asset path under the root, so two packages whose assets share
    // a file name but sit in different asset folders never collide.
    internal static string DestinationFor(string rootDirectory, string normalizedAssetPath)
        => Path.Combine(rootDirectory, normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar));

    internal static string StampPathFor(string destination) => destination + StampSuffix;
    internal static string PartialPathFor(string destination) => destination + PartialSuffix;

    // The stamp ties an extracted copy to one installed build of the application. Android bumps
    // LastUpdateTime on every install, including a developer redeploy with the same version code,
    // so a changed asset is always re-extracted and an unchanged one never is.
    internal static string Compose(long versionCode, long lastUpdateTime, string normalizedAssetPath)
        => string.Join('|', Version, versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            lastUpdateTime.ToString(System.Globalization.CultureInfo.InvariantCulture), normalizedAssetPath);

    internal static bool Matches(string storedStamp, string expectedStamp)
        => storedStamp != null && string.Equals(storedStamp.Trim(), expectedStamp, StringComparison.Ordinal);
}

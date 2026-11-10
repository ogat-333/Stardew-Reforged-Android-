using Android.App;
using Android.Content.PM;
using Android.OS;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMAPIGameLoader;

/// <summary>
/// APK locator for SMAPI Launcher 1.1.7.
///
/// Patch purpose: allow a verified single/universal Android APK to be used
/// when the Play Store split APKs (split_config.arm64 + split_content) do not
/// exist. The fallback is deliberately strict: the base APK must contain both
/// the ARM64 .NET Assembly Store and Stardew's Content assets.
/// </summary>
internal static class StardewApkTool
{
    public const string GamePlayStorePackageName = "com.chucklefish.stardewvalley";
    public const string GameGalaxyStorePackageName = "com.chucklefish.stardewvalleysamsung";

    private static bool IsGameFromPlayStore = false;
    private static bool IsGameFromGalaxyStore = false;
    private static PackageInfo _currentPackageInfo;
    private static bool? _baseApkHasUniversalArm64Layout;

    private const string Arm64AssemblyStoreEntry =
        "lib/arm64-v8a/libassemblies.arm64-v8a.blob.so";
    private const string ContentEntryPrefix = "assets/Content/";

    static StardewApkTool()
    {
        Console.WriteLine("Initialize Stardew Apk Tool");

        var playStore = ApkTool.GetPackageInfo(GamePlayStorePackageName);
        var samsung = ApkTool.GetPackageInfo(GameGalaxyStorePackageName);

        // Keep the original preference: Samsung package first, then Play Store.
        if (samsung != null)
        {
            _currentPackageInfo = samsung;
            IsGameFromGalaxyStore = true;
            Console.WriteLine("Game Install From Galaxy Store");
        }
        else if (playStore != null)
        {
            _currentPackageInfo = playStore;
            IsGameFromPlayStore = true;
            Console.WriteLine("Game Install From Play Store / compatible package ID");
        }
    }

    public static PackageInfo CurrentPackageInfo => _currentPackageInfo;

    /// <summary>
    /// Accept either the original two-split Play Store layout, or a single APK
    /// only when its ZIP directory proves it contains both required payloads.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            if (CurrentPackageInfo == null)
                return false;

            if (IsGameFromPlayStore)
            {
                var splitApks = CurrentPackageInfo.ApplicationInfo?.SplitSourceDirs;
                bool hasArm64Split = splitApks?.Any(path =>
                    path.Contains("split_config.arm64", StringComparison.OrdinalIgnoreCase)) == true;
                bool hasContentSplit = splitApks?.Any(path =>
                    path.Contains("split_content", StringComparison.OrdinalIgnoreCase)) == true;

                // Require the exact two payloads the resolver uses; a list with
                // two unrelated splits must not be mistaken for a complete install.
                if (hasArm64Split && hasContentSplit)
                    return true;

                return BaseApkHasUniversalArm64Layout;
            }

            // Preserve the existing Galaxy Store behavior.
            return true;
        }
    }

    public static Android.Content.Context GetContext => Application.Context;

    public static string? BaseApkPath => CurrentPackageInfo?.ApplicationInfo?.PublicSourceDir;

    /// <summary>
    /// True only when the installed base APK contains the ARM64 assembly blob
    /// AND at least one Stardew Content asset. Cached because APK inspection
    /// can be relatively expensive and these paths don't change during a run.
    /// </summary>
    private static bool BaseApkHasUniversalArm64Layout
    {
        get
        {
            if (_baseApkHasUniversalArm64Layout.HasValue)
                return _baseApkHasUniversalArm64Layout.Value;

            _baseApkHasUniversalArm64Layout = InspectBaseApk(BaseApkPath);
            return _baseApkHasUniversalArm64Layout.Value;
        }
    }

    private static bool InspectBaseApk(string apkPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apkPath) || !File.Exists(apkPath))
            {
                Console.WriteLine("Base APK path is missing; cannot inspect universal APK layout.");
                return false;
            }

            using (var file = File.Open(apkPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false))
            {
                bool hasArm64Assemblies = archive.GetEntry(Arm64AssemblyStoreEntry) != null;
                bool hasGameContent = archive.Entries.Any(entry =>
                    entry.FullName.StartsWith(ContentEntryPrefix, StringComparison.Ordinal));

                Console.WriteLine($"Single APK layout check: ARM64 assemblies={hasArm64Assemblies}, Content assets={hasGameContent}");
                return hasArm64Assemblies && hasGameContent;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to inspect base APK layout: {ex}");
            return false;
        }
    }

    public static string? Arm64ApkPath
    {
        get
        {
            try
            {
                if (CurrentPackageInfo == null)
                    return null;

                if (IsGameFromPlayStore)
                {
                    var arm64Split = CurrentPackageInfo.ApplicationInfo?.SplitSourceDirs?
                        .FirstOrDefault(path => path.Contains("split_config.arm64", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(arm64Split))
                        return arm64Split;

                    // Universal/single APK: the base APK itself has the ARM64 Assembly Store.
                    return BaseApkHasUniversalArm64Layout ? BaseApkPath : null;
                }

                // Samsung layout: assemblies are already in the base APK.
                return BaseApkPath;
            }
            catch (Exception ex)
            {
                ErrorDialogTool.Show(ex, "Error try to get Arm64ApkPath");
                return null;
            }
        }
    }

    public static string? ContentApkPath
    {
        get
        {
            try
            {
                if (CurrentPackageInfo == null)
                    return null;

                if (IsGameFromPlayStore)
                {
                    var contentSplit = CurrentPackageInfo.ApplicationInfo?.SplitSourceDirs?
                        .FirstOrDefault(path => path.Contains("split_content", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(contentSplit))
                        return contentSplit;

                    // Universal/single APK: Content assets are in the base APK.
                    return BaseApkHasUniversalArm64Layout ? BaseApkPath : null;
                }

                // Samsung layout: Content assets are in the base APK.
                return BaseApkPath;
            }
            catch (Exception ex)
            {
                ErrorDialogTool.Show(ex, "Error try to get ContentApkPath");
                return null;
            }
        }
    }

    public static Version GameVersionSupport
    {
        get
        {
            if (CurrentPackageInfo == null)
                return null;

            switch (CurrentPackageInfo.PackageName)
            {
                case GamePlayStorePackageName:
                    return new Version(1, 6, 15, 3);
                case GameGalaxyStorePackageName:
                    return new Version(1, 6, 15, 3);
                default:
                    return null;
            }
        }
    }

    public static Version CurrentGameVersion
    {
        get
        {
            try
            {
                return new Version(CurrentPackageInfo?.VersionName);
            }
            catch (Exception)
            {
                return new Version(0, 0, 0, 0);
            }
        }
    }

    public static bool IsGameVersionSupport =>
        GameVersionSupport != null && CurrentGameVersion >= GameVersionSupport;
}
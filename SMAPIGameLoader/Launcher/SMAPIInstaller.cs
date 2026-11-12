using Octokit;
using SMAPIGameLoader.Tool;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace SMAPIGameLoader.Launcher;

internal static class SMAPIInstaller
{
    public const string GithubOwner = "NRTnarathip";
    public const string GithubRepoName = "SMAPI-Android-1.6";
    public static long GetBuildCode()
    {
        try
        {
            if (!File.Exists(GetInstallFilePath))
            {
                return 0;
            }

            return ReadBuildCodeFromAssembly(GetInstallFilePath);
        }
        catch (Exception ex)
        {
            //ErrorDialogTool.Show(ex);
            return 0;
        }
    }

    public static Version GetCurrentVersion()
    {
        try
        {
            if (!File.Exists(GetInstallFilePath))
            {
                return new Version(0, 0, 0, 0);
            }

            return ReadVersionFromAssembly(GetInstallFilePath);
        }
        catch
        {
            return new Version(0, 0, 0, 0);
        }
    }


#if false
    public static async void OnClickInstallSMAPIOnline()
    {
        try
        {
            TaskTool.Run(ActivityTool.CurrentActivity, async () =>
            {
                try
                {
                    TaskTool.SetTitle("Install SMAPI Online");
                    var github = new GitHubClient(new ProductHeaderValue("SMPAI-Installer"));
                    TaskTool.NewLine("try get all release..");
                    var releases = await github.Repository.Release.GetAll(GithubOwner, GithubRepoName);
                    TaskTool.NewLine("found release count: " + releases.Count);
                    var latestRelease = releases.FirstOrDefault();
                    if (latestRelease is null)
                    {
                        ErrorDialogTool.Show(new Exception("Failed install SMAPI, not found any release file"));
                        return;
                    }

                    var smapiAssetFile = latestRelease.Assets.FirstOrDefault(
                         asset => asset.Name.StartsWith("SMAPI-")
                         && asset.Name.EndsWith(".zip"));

                    if (smapiAssetFile != null)
                    {
                        TaskTool.NewLine("found SMAPI latest file: " + smapiAssetFile.Name);
                        var smapiZipFilePath = Path.Combine(FileTool.ExternalFilesDir, smapiAssetFile.Name);
                        TaskTool.NewLine("starting download & install");
                        TaskTool.NewLine($"file size: {FileTool.ConvertBytesToMB(smapiAssetFile.Size):F2} MB");

                        using (var netClient = new HttpClient())
                        {
                            Console.WriteLine($"Retrieving {smapiAssetFile.Name}");
                            var fileData = await netClient.GetByteArrayAsync(smapiAssetFile.BrowserDownloadUrl);
                            File.WriteAllBytes(smapiZipFilePath, fileData);
                            Console.WriteLine("done save zip file at: " + smapiZipFilePath + ", file size: " + fileData.Length);
                        }

                        InstallSMAPIFromZipFile(smapiZipFilePath);

                        TaskTool.NewLine("Successfully install SMAPI: " + smapiAssetFile.Name);
                        DialogTool.Show("Successfully Install SMAPI",
                            $"done install zip: {smapiAssetFile.Name}." +
                            $"\nyou can close this");
                    }
                    else
                    {
                        TaskTool.NewLine("Not found any SMAPI");
                    }

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    ErrorDialogTool.Show(ex);
                    Console.WriteLine("error try to install SMAPI Zip: " + ex);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorDialogTool.Show(ex);
            Console.WriteLine("error try to install SMAPI Zip: " + ex);
        }
    }
#endif

    static bool IsSMAPIZipFromPickFile(FileResult pick)
    {
        var fileName = pick.FileName;

        if (fileName.EndsWith(".zip") is false)
            return false;

        if (fileName.Contains("SMAPI") && fileName.Contains("Android"))
        {
            //check file size should less than PC
            //on PC SMAPI-4.1.10-installer-for-developers.zip
            //file size 40mb

            //on Android SMAPI-4.1.10.2-(1735397682).zip
            //file size 1.5mb
            var fileInfo = new FileInfo(pick.FullPath);

            //less than 30mb
            return FileTool.ConvertBytesToMB(fileInfo.Length) <= 10;
        }


        return false;
    }

    public static Action OnInstalledSMAPI;
    public static async void OnClickInstallSMAPIZip(object sender, EventArgs eventArgs)
    {
        try
        {

            var pick = await FilePickerTool.PickZipFile(title: "Please Pick File SMAPI-4.x.x.xxxx.zip Android");
            if (pick == null)
                return;

            //assert SMAPI it's android
            if (IsSMAPIZipFromPickFile(pick) is false)
            {
                DialogTool.Show("SMAPI Installer Error", "Please select file SMAPI-4.x.x.xxxx.zip for Android");
                return;
            }

            InstallSMAPIFromZipFile(pick.FullPath);

            if (!IsInstalled)
                throw new InvalidDataException("SMAPI verification failed: DLL or version/build metadata is missing or invalid.");

            OnInstalledSMAPI?.Invoke();
            DialogTool.Show("SMAPI installed and verified",
                $"Installed SMAPI {GetCurrentVersion()} (Build {GetBuildCode()}) from {pick.FileName}.");

        }
        catch (Exception ex)
        {
            ErrorDialogTool.Show(ex);
            Console.WriteLine("SMAPI installation failed: " + ex);
        }
        finally
        {
            OnInstalledSMAPI?.Invoke();
            OnInstalledSMAPI = null;
        }
    }
    static void InstallSMAPIFromZipFile(string smapiZipFilePath)
    {
        var stardewDir = GameAssemblyManager.AssembliesDirPath;
        Directory.CreateDirectory(stardewDir);

        var stagingDir = Path.Combine(stardewDir, ".smapi-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        try
        {
            using (var zip = ZipFile.OpenRead(smapiZipFilePath))
            {
                var fileEntries = zip.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.FullName) && !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    .ToList();

                if (fileEntries.Count == 0)
                    throw new InvalidDataException("The selected SMAPI ZIP is empty.");

                // The official Android package currently has one wrapper folder.
                // Detect it from the archive instead of assuming every entry has a parent path.
                var normalized = new List<(ZipArchiveEntry Entry, string[] Parts)>();
                foreach (var entry in fileEntries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    if (name.StartsWith("/", StringComparison.Ordinal) || name.Contains(":"))
                        throw new InvalidDataException("The SMAPI ZIP contains an unsafe absolute path: " + entry.FullName);

                    var parts = name.Split('/');
                    if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
                        throw new InvalidDataException("The SMAPI ZIP contains an unsafe path: " + entry.FullName);

                    normalized.Add((entry, parts));
                }

                var firstSegment = normalized[0].Parts[0];
                var hasSingleRoot = normalized.All(item =>
                    item.Parts.Length > 1 &&
                    string.Equals(item.Parts[0], firstSegment, StringComparison.Ordinal));

                var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in normalized)
                {
                    var relativeParts = hasSingleRoot ? item.Parts.Skip(1).ToArray() : item.Parts;
                    if (relativeParts.Length == 0)
                        continue;

                    var relativePath = Path.Combine(relativeParts);
                    var destination = Path.GetFullPath(Path.Combine(stagingDir, relativePath));
                    var stagingRoot = Path.GetFullPath(stagingDir) + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(stagingRoot, StringComparison.Ordinal))
                        throw new InvalidDataException("The SMAPI ZIP entry escapes the installation directory: " + item.Entry.FullName);

                    if (!extractedPaths.Add(relativePath))
                        throw new InvalidDataException("The SMAPI ZIP contains duplicate file paths: " + relativePath);

                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    using var source = item.Entry.Open();
                    using var target = new FileStream(destination, System.IO.FileMode.Create, FileAccess.Write, FileShare.None);
                    source.CopyTo(target);
                }
            }

            var stagedMainAssembly = Path.Combine(stagingDir, StardewModdingAPIFileName);
            var stagedInternal = Path.Combine(stagingDir, "smapi-internal");
            foreach (var requiredFile in new[]
            {
                stagedMainAssembly,
                Path.Combine(stagedInternal, "config.json"),
                Path.Combine(stagedInternal, "blacklist.json"),
                Path.Combine(stagedInternal, "metadata.json"),
                Path.Combine(stagedInternal, "i18n", "default.json")
            })
            {
                if (!File.Exists(requiredFile))
                    throw new InvalidDataException("The SMAPI ZIP is incomplete. Missing: " + Path.GetFileName(requiredFile));
            }

            var stagedVersion = ReadVersionFromAssembly(stagedMainAssembly);
            var stagedBuildCode = ReadBuildCodeFromAssembly(stagedMainAssembly);
            if (stagedVersion <= new Version(0, 0, 0, 0) || stagedBuildCode <= 0)
                throw new InvalidDataException("SMAPI version/build metadata is missing or invalid in the selected ZIP.");

            // Only replace the installed files after the complete archive has passed validation.
            var installedInternal = Path.Combine(stardewDir, "smapi-internal");
            if (Directory.Exists(installedInternal))
                Directory.Delete(installedInternal, true);

            foreach (var sourceFile in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stagingDir, sourceFile);
                var destination = Path.Combine(stardewDir, relative);
                var parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                File.Copy(sourceFile, destination, true);
            }

            if (!IsInstalled)
                throw new InvalidDataException("SMAPI files were copied, but the installed DLL failed final verification.");

            Console.WriteLine($"Verified SMAPI {GetCurrentVersion()} build {GetBuildCode()} at {GetInstallFilePath}");
        }
        finally
        {
            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, true);
        }
    }

    static long ReadBuildCodeFromAssembly(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream);
        var buildType = assembly.MainModule.Types.Single(t => t.FullName == "StardewModdingAPI.Mobile.SMAPIAndroidBuild");
        var buildString = buildType.Fields.Single(field => field.Name == "BuildCode").Constant as string;
        return long.Parse(buildString);
    }

    static Version ReadVersionFromAssembly(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream);
        var constantsType = assembly.MainModule.Types.Single(t => t.FullName == "StardewModdingAPI.EarlyConstants");
        var version = constantsType.Fields.Single(field => field.Name == "RawApiVersionForAndroid").Constant as string;
        return new Version(version);
    }
    public static void UninstallSMAPI()
    {
        if (IsInstalled is false)
            return;
        File.Delete(GetInstallFilePath);
    }

    public const string StardewModdingAPIFileName = "StardewModdingAPI.dll";
    public static string GetInstallFilePath => Path.Combine(GameAssemblyManager.AssembliesDirPath, StardewModdingAPIFileName);
    public static bool IsInstalled => File.Exists(GetInstallFilePath) && GetCurrentVersion() > new Version(0, 0, 0, 0) && GetBuildCode() > 0;
}

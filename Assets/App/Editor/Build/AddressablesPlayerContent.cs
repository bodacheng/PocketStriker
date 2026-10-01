using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;

namespace Cocone.ProjectP3
{
    /// <summary>Uses the catalog and local bundles published by the independent asset job.</summary>
    internal static class AddressablesPlayerContent
    {
        internal const string ArchiveName = "player-bootstrap.zip";
        private const string RuntimePathToken = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/";

        internal static void Prepare(AddressableAssetSettings settings, BuildTarget buildTarget)
        {
            if (settings == null || !settings.BuildRemoteCatalog || settings.DisableCatalogUpdateOnStartup)
                throw new BuildFailedException("Addressables must build a remote catalog and check it at startup.");
            if (EditorUserBuildSettings.activeBuildTarget != buildTarget)
                throw new BuildFailedException($"Start Unity with -buildTarget {buildTarget} before preparing player resources.");

            var remotePath = (settings.RemoteCatalogLoadPath.GetValue(settings) ?? string.Empty).TrimEnd('/');
            if (!Uri.TryCreate(remotePath, UriKind.Absolute, out var remoteUri) || remoteUri.Scheme != Uri.UriSchemeHttps)
                throw new BuildFailedException("Remote.LoadPath must resolve to an HTTPS asset URL.");
            var archiveUrl = remotePath + "/" + ArchiveName;
            var destination = Path.GetFullPath(Addressables.BuildPath);
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "PocketStrikerAddressables-" + Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(temporaryRoot, ArchiveName);
            var extractedPath = Path.Combine(temporaryRoot, "content");
            Directory.CreateDirectory(extractedPath);
            try
            {
                Debug.Log($"Downloading published Addressables player content: {archiveUrl}");
                var request = (HttpWebRequest)WebRequest.Create(archiveUrl);
                request.Timeout = PocketStrikerDownloadPolicy.RequestTimeoutSeconds * 1000;
                request.ReadWriteTimeout = PocketStrikerDownloadPolicy.RequestTimeoutSeconds * 1000;
                using (var response = request.GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = File.Create(archivePath))
                    input.CopyTo(output);

                ExtractArchive(archivePath, extractedPath);
                Validate(extractedPath, buildTarget, remotePath, settings.PlayerBuildVersion);

                // The old Library cache may be from another profile, version or local
                // validation build. Replace it only after the published archive passes.
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                CopyDirectory(extractedPath, destination);
                Debug.Log($"Using published Addressables catalog for {buildTarget}: {remotePath}");
            }
            catch (Exception exception)
            {
                throw new BuildFailedException($"Could not prepare published assets from {archiveUrl}. Run the independent asset job for this profile, version and platform first.\n{exception.Message}");
            }
            finally
            {
                if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            }
        }

        internal static void ExtractArchive(string archivePath, string destination)
        {
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName;
                    var components = name.TrimEnd('/').Split('/');
                    if (name.Length == 0 || name.Contains("\\") || name.Contains(":") || name.StartsWith("/") ||
                        components.Any(component => component.Length == 0 || component == "." || component == "..") ||
                        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !paths.Add(name.TrimEnd('/')))
                        throw new InvalidDataException($"Unsafe or duplicate bootstrap archive path: {name}");
                    var path = Path.GetFullPath(Path.Combine(destination, name));
                    if (!path.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException($"Bootstrap archive path escapes its directory: {name}");
                    if (name.EndsWith("/")) Directory.CreateDirectory(path);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        entry.ExtractToFile(path);
                    }
                }
            }
        }

        internal static void Validate(string directory, BuildTarget target, string remotePath, string version)
        {
            RequireFile(directory, "settings.json");
            RequireFile(directory, "catalog.hash");
            RequireFile(directory, "AddressablesLink/link.xml");
            var runtime = JsonUtility.FromJson<ResourceManagerRuntimeData>(File.ReadAllText(Path.Combine(directory, "settings.json")));
            if (runtime == null || runtime.BuildTarget != target.ToString() || runtime.AddressablesVersion != Addressables.Version ||
                runtime.DisableCatalogUpdateOnStartup)
                throw new InvalidDataException("Published bootstrap has a different platform or Addressables version, or disables catalog updates.");
            var remoteHash = remotePath.TrimEnd('/') + "/catalog_" + version + ".hash";
            var remoteLocation = runtime.CatalogLocations.SingleOrDefault(location => location.InternalId == remoteHash);
            var catalog = runtime.CatalogLocations.SingleOrDefault(location =>
                location.Keys != null && location.Keys.Contains(ResourceManagerRuntimeData.kCatalogAddress));
            if (remoteLocation == null || catalog == null || catalog.Dependencies == null || remoteLocation.Keys == null ||
                !remoteLocation.Keys.Any(key => catalog.Dependencies.Contains(key)) ||
                (catalog.InternalId != RuntimePathToken + "catalog.bin" && catalog.InternalId != RuntimePathToken + "catalog.json"))
                throw new InvalidDataException("Published bootstrap does not reference the selected remote catalog URL.");
            RequireFile(directory, catalog.InternalId.Substring(RuntimePathToken.Length));
            if (!Regex.IsMatch(File.ReadAllText(Path.Combine(directory, "catalog.hash")).Trim(), @"\A[0-9a-fA-F]{32}\z"))
                throw new InvalidDataException("Published bootstrap catalog.hash is invalid.");
            using (var reader = XmlReader.Create(Path.Combine(directory, "AddressablesLink/link.xml"),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                reader.MoveToContent();
                if (reader.Name != "linker") throw new InvalidDataException("Published bootstrap link.xml has no linker root.");
                while (reader.Read()) { }
            }
        }

        private static void RequireFile(string directory, string relativePath)
        {
            var path = Path.Combine(directory, relativePath);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new InvalidDataException($"Published bootstrap is missing {relativePath}.");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}

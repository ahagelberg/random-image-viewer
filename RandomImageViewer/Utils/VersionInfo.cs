using System;
using System.IO;
using System.Reflection;

namespace RandomImageViewer.Utils
{
    /// <summary>
    /// Reads version and metadata information from version.txt
    /// </summary>
    public static class VersionInfo
    {
        private static readonly Lazy<VersionData> _versionData = new Lazy<VersionData>(LoadVersionData);
        
        public static VersionData Data => _versionData.Value;

        private static VersionData LoadVersionData()
        {
            try
            {
                // Look for version.txt in the project root (two levels up from bin)
                var versionFile = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "version.txt"
                );

                if (!File.Exists(versionFile))
                {
                    // Fallback: look in current directory
                    versionFile = "version.txt";
                }

                if (!File.Exists(versionFile))
                {
                    // Fallback: use assembly version
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    return new VersionData
                    {
                        Version = $"{version.Major}.{version.Minor}.{version.Build}",
                        Major = version.Major,
                        Minor = version.Minor,
                        Patch = version.Build,
                        Revision = version.Revision,
                        AppName = "Random Image Viewer",
                        AppDescription = "A Windows application for viewing images in random order",
                        Company = "Your Company",
                        Copyright = $"Copyright © {DateTime.Now.Year}",
                        Product = "Random Image Viewer"
                    };
                }

                var lines = File.ReadAllLines(versionFile);
                var data = new VersionData();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key.ToUpper())
                    {
                        case "VERSION":
                            data.Version = value;
                            break;
                        case "MAJOR":
                            if (int.TryParse(value, out int major))
                                data.Major = major;
                            break;
                        case "MINOR":
                            if (int.TryParse(value, out int minor))
                                data.Minor = minor;
                            break;
                        case "PATCH":
                            if (int.TryParse(value, out int patch))
                                data.Patch = patch;
                            break;
                        case "REVISION":
                            if (int.TryParse(value, out int revision))
                                data.Revision = revision;
                            break;
                        case "APP_NAME":
                            data.AppName = value;
                            break;
                        case "APP_DESCRIPTION":
                            data.AppDescription = value;
                            break;
                        case "COMPANY":
                            data.Company = value;
                            break;
                        case "COPYRIGHT":
                            data.Copyright = value;
                            break;
                        case "PRODUCT":
                            data.Product = value;
                            break;
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading version info: {ex.Message}");
                // Return default version
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return new VersionData
                {
                    Version = $"{version.Major}.{version.Minor}.{version.Build}",
                    Major = version.Major,
                    Minor = version.Minor,
                    Patch = version.Build,
                    Revision = version.Revision,
                    AppName = "Random Image Viewer",
                    AppDescription = "A Windows application for viewing images in random order",
                    Company = "Your Company",
                    Copyright = $"Copyright © {DateTime.Now.Year}",
                    Product = "Random Image Viewer"
                };
            }
        }
    }

    public class VersionData
    {
        public string Version { get; set; } = "1.0.0";
        public int Major { get; set; } = 1;
        public int Minor { get; set; } = 0;
        public int Patch { get; set; } = 0;
        public int Revision { get; set; } = 0;
        public string AppName { get; set; } = "Random Image Viewer";
        public string AppDescription { get; set; } = "A Windows application for viewing images in random order";
        public string Company { get; set; } = "Your Company";
        public string Copyright { get; set; } = "Copyright © 2024";
        public string Product { get; set; } = "Random Image Viewer";
    }
}


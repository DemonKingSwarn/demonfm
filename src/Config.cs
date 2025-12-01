using System;
using System.IO;

namespace demonfm.ConfigManager
{
    public class Config
    {
        public bool ShowHiddenFiles { get; set; } = false;

        private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "demonfm");
        private static string ConfigPath => Path.Combine(ConfigDir, "config.toml");

        public static Config Load()
        {
            var config = new Config();
            if (!File.Exists(ConfigPath)) return config;

            try
            {
                var lines = File.ReadAllLines(ConfigPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("show_hidden_files"))
                    {
                        var parts = trimmed.Split('=');
                        if (parts.Length == 2)
                        {
                            string valStr = parts[1].Trim().ToLower();
                            if (valStr == "true") config.ShowHiddenFiles = true;
                            else if (valStr == "false") config.ShowHiddenFiles = false;
                        }
                    }
                }
            }
            catch 
            { 
                // Fallback to defaults on error
            }
            return config;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(ConfigPath, $"show_hidden_files = {ShowHiddenFiles.ToString().ToLower()}");
            }
            catch 
            {
                // Ignore save errors for now or handle UI error if possible, 
            }
        }
    }
}

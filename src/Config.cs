using System;
using System.IO;

namespace demonfm.ConfigManager
{
    public class Config
    {
        public bool ShowHiddenFiles { get; set; } = false;
        public string ChafaBackend { get; set; } = "";

        private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "demonfm");
        private static string ConfigPath => Path.Combine(ConfigDir, "config.toml");

        public static Config Load()
        {
            var config = new Config();
            if (!File.Exists(ConfigPath)) return config;

            try
            {
                var lines = File.ReadAllLines(ConfigPath);
                bool inChafaSection = false;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        inChafaSection = trimmed == "[chafa]";
                        continue;
                    }

                    if (inChafaSection)
                    {
                        if (trimmed.StartsWith("backend"))
                        {
                            var parts = trimmed.Split('=');
                            if (parts.Length == 2)
                            {
                                config.ChafaBackend = parts[1].Trim().Trim('"');
                            }
                        }
                    }
                    else
                    {
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
            }
            catch 
            { 
            }
            return config;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"show_hidden_files = {ShowHiddenFiles.ToString().ToLower()}");
                sb.AppendLine();
                sb.AppendLine("[chafa]");
                sb.AppendLine($"backend = \"{ChafaBackend}\"");
                
                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch 
            {
            }
        }
    }
}

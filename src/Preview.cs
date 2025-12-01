using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace demonfm.Preview
{
    public static class PreviewGenerator
    {
        private static bool? hasBat;

        public static (List<string> Lines, string? ImagePath) GetPreview(FileSystemInfo selected, int maxLines, bool isKittyTerminal, bool showHiddenFiles)
        {
            var previewLines = new List<string>();

            try
            {
                if (selected is DirectoryInfo dir)
                {
                    try
                    {
                        var entries = dir.GetFileSystemInfos().OrderBy(e => e.Name).AsEnumerable();
                        
                        if (!showHiddenFiles)
                        {
                            entries = entries.Where(e => !e.Name.StartsWith("."));
                        }
                        
                        foreach (var entry in entries.Take(maxLines))
                        {
                            if (entry is DirectoryInfo)
                            {
                                previewLines.Add(entry.Name + "/");
                            }
                            else
                            {
                                previewLines.Add(entry.Name);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        previewLines.Add("Access Denied");
                    }
                }
                else if (selected is FileInfo file)
                {
                    string ext = file.Extension.ToLower();

                    // Image handling
                    string[] imageExtensions = { 
                        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".avif", ".jxl", 
                        ".ico", ".tiff", ".tif", ".svg", ".heic", ".heif", ".pbm", ".pgm", 
                        ".ppm", ".tga", ".cur", ".ani", ".pam", ".pcx" 
                    };
                    if (imageExtensions.Contains(ext))
                    {
                        if (isKittyTerminal)
                        {
                            return (previewLines, file.FullName);
                        }
                        else
                        {
                             previewLines.Add("Image file (No preview)");
                             return (previewLines, null);
                        }
                    }

                    if (IsMediaFile(ext))
                    {
                        var mediaInfo = TryRunCommand("mediainfo", $"\"{file.FullName}\"");
                        if (mediaInfo != null && mediaInfo.Count > 0) 
                        {
                            return (mediaInfo.Take(maxLines).ToList(), null);
                        }
                    }

                    if (ext == ".zip")
                    {
                        try 
                        {
                            using (var zip = ZipFile.OpenRead(file.FullName))
                            {
                                foreach (var entry in zip.Entries.Take(maxLines))
                                {
                                    previewLines.Add(entry.FullName);
                                }
                            }
                            return (previewLines, null);
                        }
                        catch (Exception ex) { previewLines.Add($"Zip Error: {ex.Message}"); return (previewLines, null); }
                    }
                    else if (ext == ".tar" || ext == ".gz" || ext == ".tgz")
                    {
                         var tarOut = TryRunCommand("tar", $"-tf \"{file.FullName}\"");
                         if (tarOut != null) return (tarOut.Take(maxLines).ToList(), null);
                    }
                    else if (ext == ".7z" || ext == ".rar")
                    {
                         var sevenZOut = TryRunCommand("7z", $"l \"{file.FullName}\"");
                         if (sevenZOut != null) return (sevenZOut.Take(maxLines).ToList(), null);
                    }

                    // Standard File Handling
                    string[] binaryExtensions = { ".exe", ".dll", ".bin", ".iso", ".pdf" };
                    if (binaryExtensions.Contains(ext))
                    {
                        previewLines.Add("Binary file");
                    }
                    else if (file.Length > 1024 * 1024 * 5) // > 5MB
                    {
                        previewLines.Add("File too large to preview");
                    }
                    else
                    {
                         // Check for bat
                         if (hasBat == null)
                         {
                             var check = TryRunCommand("bat", "--version");
                             hasBat = (check != null && check.Count > 0);
                         }

                         if (hasBat == true)
                         {
                             var batOut = TryRunCommand("bat", $"--color=always --style=plain --paging=never --line-range=:{maxLines} \"{file.FullName}\"");
                             if (batOut != null)
                             {
                                 // bat might return more lines than requested or empty, just add
                                 previewLines.AddRange(batOut.Take(maxLines));
                             }
                             else 
                             {
                                 // Fallback if bat fails (e.g. runtime error)
                                 foreach (var line in File.ReadLines(file.FullName).Take(maxLines))
                                 {
                                     previewLines.Add(line);
                                 }
                             }
                         }
                         else
                         {
                             foreach (var line in File.ReadLines(file.FullName).Take(maxLines))
                             {
                                 previewLines.Add(line);
                             }
                         }
                    }
                }
            }
            catch (Exception ex)
            {
                previewLines.Add($"Error reading preview: {ex.Message}");
            }

            return (previewLines, null);
        }

        private static bool IsMediaFile(string ext)
        {
            var mediaExts = new HashSet<string> { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".wma", ".aac" };
            return mediaExts.Contains(ext);
        }

        private static List<string>? TryRunCommand(string command, string args)
        {
            try 
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                if (process == null) return null;
                
                var output = new List<string>();
                while (!process.StandardOutput.EndOfStream)
                {
                    string? line = process.StandardOutput.ReadLine();
                    if (line != null) output.Add(line);
                    if (output.Count > 200) break; // Safety limit
                }
                process.WaitForExit(100); // Short wait
                return output;
            }
            catch { return null; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace demonfm.zipper
{
    public static class Zipper
    {
        public static void Extract(string filePath, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            string extension = Path.GetExtension(filePath).ToLower();
            
            string command = "";
            string arguments = "";

            if (extension == ".zip")
            {
                command = "unzip";
                arguments = $"\"{filePath}\" -d \"{destinationDir}\"";
            }
            else if (extension == ".7z" || extension == ".rar")
            {
                command = "7z";
                arguments = $"x \"{filePath}\" -o\"{destinationDir}\"" ;
            }
            else if (extension == ".tar" || filePath.EndsWith(".tar.gz") || filePath.EndsWith(".tgz") || filePath.EndsWith(".tar.xz") || filePath.EndsWith(".txz"))
            {
                command = "tar";
                arguments = $"-xf \"{filePath}\" -C \"{destinationDir}\"" ;
            }
            else
            {
                 // Fallback to 7z for other formats if possible, or just try tar
                 // Given the strict instruction, I'll assume other tar formats are caught by tar
                 // If unknown, maybe default to 7z as it handles many
                 command = "7z";
                 arguments = $"x \"{filePath}\" -o\"{destinationDir}\"" ;
            }

            RunCommand(command, arguments);
        }

        public static void Compress(IEnumerable<string> sourcePaths, string destinationArchive)
        {
            string extension = Path.GetExtension(destinationArchive).ToLower();
            // specific check for double extensions like .tar.gz
            if (destinationArchive.EndsWith(".tar.gz") || destinationArchive.EndsWith(".tgz") || 
                destinationArchive.EndsWith(".tar.xz") || destinationArchive.EndsWith(".txz"))
            {
                extension = ".tar"; 
            }

            string command = "";
            string arguments = "";
            
            string sources = string.Join(" ", sourcePaths.Select(p => $"\"{p}\"" ));

            if (extension == ".zip")
            {
                command = "zip";
                arguments = $"-r \"{destinationArchive}\" {sources}";
            }
            else if (extension == ".7z" || extension == ".rar")
            {
                command = "7z";
                arguments = $"a \"{destinationArchive}\" {sources}";
            }
            else if (extension == ".tar")
            {
                command = "tar";
                string flags = "-cf";
                if (destinationArchive.EndsWith(".tar.gz") || destinationArchive.EndsWith(".tgz")) flags = "-czf";
                if (destinationArchive.EndsWith(".tar.xz") || destinationArchive.EndsWith(".txz")) flags = "-cJf"; // J for xz
                
                arguments = $"{flags} \"{destinationArchive}\" {sources}";
            }
            else
            {
                // Default to zip
                 command = "zip";
                 arguments = $"-r \"{destinationArchive}\" {sources}";
            }

            RunCommand(command, arguments);
        }

        private static void RunCommand(string command, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = false, 
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                using (var process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                // Let the caller handle or UI display errors if needed. 
                // Or simpler: write to console since we might be in a "restored" state or about to redraw
                Console.WriteLine($"Error running {command}: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}

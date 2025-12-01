using System;
using System.Diagnostics;

namespace demonfm.fzf
{
    public class FzfSelector
    {
        public static string? RunFzf(string workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "fzf",
                    //Arguments = "--height 40% --layout=reverse --border",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                string result = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool CheckFzfInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "fzf",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}


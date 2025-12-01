using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using demonfm.UI;
using demonfm.Preview;
using demonfm.ConfigManager;

namespace demonfm.filemanager
{
    public class FileManager
    {
        public static string? currentPath;
        public static List<FileSystemInfo>? items;
        public static int selectedIndex;
        public static int scrollOffset;
        public static bool running = true;

        private Ui ui;
        private bool isKittyTerminal;
        private Config config;

        public FileManager()
        {
            ui = new Ui();
            config = new Config();
        }

        public void Initialize()
        {
            config = Config.Load();
            currentPath = Directory.GetCurrentDirectory();
            selectedIndex = 0;
            scrollOffset = 0;
            
            string? term = Environment.GetEnvironmentVariable("TERM");
            string? termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            string? konsoleVersion = Environment.GetEnvironmentVariable("KONSOLE_VERSION");
            
            isKittyTerminal = (term != null && term.Contains("kitty")) || 
                              (termProgram != null && (termProgram.Contains("WezTerm") || termProgram.Contains("ghostty"))) ||
                              !string.IsNullOrEmpty(konsoleVersion);
            
            Console.CursorVisible = false;
            Console.Title = "DemonFM";
            RefreshItems();
            Console.Clear();
        }

        public void RefreshItems()
        {
            if (currentPath == null) return;
            var dirInfo = new DirectoryInfo(currentPath);
            items = new List<FileSystemInfo>();

            try
            {
                var dirs = dirInfo.GetDirectories().AsEnumerable();
                var files = dirInfo.GetFiles().AsEnumerable();

                if (!config.ShowHiddenFiles)
                {
                    dirs = dirs.Where(d => !d.Name.StartsWith("."));
                    files = files.Where(f => !f.Name.StartsWith("."));
                }

                items.AddRange(dirs.OrderBy(d => d.Name));
                items.AddRange(files.OrderBy(f => f.Name));
            }
            catch (UnauthorizedAccessException)
            {
                ui.DisplayError("Access Denied, press any key to return.");
                NavigateUp();
                return;
            }

            if (selectedIndex >= items.Count && items.Count > 0) selectedIndex = items.Count - 1;
            else if (items.Count == 0) selectedIndex = 0;
            
            if (scrollOffset > Math.Max(0, items.Count - ui.GetListHeight()))
            {
                scrollOffset = Math.Max(0, items.Count - ui.GetListHeight());
            }
        }

        public void Draw()
        {
            (List<string> Lines, string? ImagePath) preview = (new List<string>(), null);
            
            if (items != null && items.Count > 0 && selectedIndex < items.Count)
            {
                 var selected = items[selectedIndex];
                 int maxLines = ui.GetListHeight();
                 preview = PreviewGenerator.GetPreview(selected, maxLines, isKittyTerminal, config.ShowHiddenFiles);
            }

            ui.Draw(currentPath, items, selectedIndex, scrollOffset, preview.Lines, preview.ImagePath);
        }

        public void HandleInput()
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        if (selectedIndex < scrollOffset)
                            scrollOffset--;
                    }
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    if (items != null && selectedIndex < items.Count - 1)
                    {
                        selectedIndex++;
                        int listHeight = ui.GetListHeight();
                        if (selectedIndex >= scrollOffset + listHeight)
                            scrollOffset++;
                    }
                    break;
                case ConsoleKey.Enter:
                case ConsoleKey.RightArrow:
                case ConsoleKey.L:
                    OpenSelectedItem();
                    break;
                case ConsoleKey.Backspace:
                case ConsoleKey.LeftArrow:
                    NavigateUp();
                    break;
                case ConsoleKey.H:
                    NavigateUp();
                    break;
                case ConsoleKey.I:
                    config.ShowHiddenFiles = !config.ShowHiddenFiles;
                    config.Save();
                    RefreshItems();
                    break;
                case ConsoleKey.R:
                    RenameItem();
                    break;
                case ConsoleKey.D:
                    DeleteItem();
                    break;
                case ConsoleKey.A:
                    CreateItem();
                    break;
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    running = false;
                    break;
                case ConsoleKey.Home:
                    selectedIndex = 0;
                    scrollOffset = 0;
                    break;
                case ConsoleKey.End:
                    if (items != null)
                    {
                        selectedIndex = items.Count - 1;
                        int h = ui.GetListHeight();
                        scrollOffset = Math.Max(0, items.Count - h);
                    }
                    break;
                case ConsoleKey.G:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    {
                        if (items != null)
                        {
                            selectedIndex = items.Count - 1;
                            int h = ui.GetListHeight();
                            scrollOffset = Math.Max(0, items.Count - h);
                        }
                    }
                    else
                    {
                        selectedIndex = 0;
                        scrollOffset = 0;
                    }
                    break;
            }
        }

        public void RenameItem()
        {
            if (items == null || items.Count == 0) return;
            var selected = items[selectedIndex];

            string? newName = ui.ReadInput($"Rename '{selected.Name}' to: ");
            if (string.IsNullOrWhiteSpace(newName)) return;

            string newPath = Path.Combine(currentPath!, newName);

            try
            {
                if (selected is DirectoryInfo)
                {
                    Directory.Move(selected.FullName, newPath);
                }
                else
                {
                    File.Move(selected.FullName, newPath);
                }
                RefreshItems();
            }
            catch (Exception ex)
            {
                ui.DisplayError($"Rename failed: {ex.Message}");
            }
        }

        public void DeleteItem()
        {
            if (items == null || items.Count == 0) return;
            var selected = items[selectedIndex];

            bool confirm = ui.GetConfirmation($"Delete '{selected.Name}'?");
            if (!confirm) return;

            try
            {
                if (selected is DirectoryInfo dir)
                {
                    dir.Delete(true);
                }
                else
                {
                    selected.Delete();
                }
                
                if (selectedIndex >= items.Count - 1 && selectedIndex > 0) selectedIndex--;
                
                RefreshItems();
            }
            catch (Exception ex)
            {
                ui.DisplayError($"Delete failed: {ex.Message}");
            }
        }

        public void CreateItem()
        {
            if (currentPath == null) return;

            string? input = ui.ReadInput("Create (end with / for dir): ");
            if (string.IsNullOrWhiteSpace(input)) return;

            string fullPath = Path.Combine(currentPath, input);

            try
            {
                if (input.EndsWith("/") || input.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    string? dirName = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                    {
                        Directory.CreateDirectory(dirName);
                    }
                    File.Create(fullPath).Close();
                }
                RefreshItems();
            }
            catch (Exception ex)
            {
                ui.DisplayError($"Create failed: {ex.Message}");
            }
        }

        public void OpenSelectedItem()
        {
            if (items == null || items.Count == 0) return;

            var selected = items[selectedIndex];

            if (selected is DirectoryInfo dir)
            {
                try
                {
                    dir.GetFiles();
                    currentPath = selected.FullName;
                    selectedIndex = 0;
                    scrollOffset = 0;
                    RefreshItems();
                }
                catch (UnauthorizedAccessException)
                {
                    ui.DisplayError($"Cannot access {selected.Name}: Access Denied");
                }
            }
            else if (selected is FileInfo file)
            {
                if (!IsBinary(file))
                {
                    // Open in EDITOR
                    string editor = Environment.GetEnvironmentVariable("EDITOR") ?? "nano";
                    try
                    {
                        Console.Clear();
                        var psi = new ProcessStartInfo
                        {
                            FileName = editor,
                            Arguments = $"\"{file.FullName}\"",
                            UseShellExecute = false
                        };
                        var process = Process.Start(psi);
                        process?.WaitForExit();
                        
                        // Restore TUI
                        Initialize();
                    }
                    catch (Exception ex)
                    {
                        Initialize(); // Ensure we restore even if error
                        ui.DisplayError($"Could not open editor: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = selected.FullName,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        ui.DisplayError($"Could not open file: {ex.Message}");
                    }
                }
            }
        }

        private bool IsBinary(FileInfo file)
        {
            string[] binaryExtensions = { ".exe", ".dll", ".bin", ".iso", ".zip", ".tar", ".gz", ".7z", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".pdf" };
            return binaryExtensions.Contains(file.Extension.ToLower());
        }

        public void NavigateUp()
        {
            if (currentPath == null) return;
            var parent = Directory.GetParent(currentPath);
            if (parent != null)
            {
                currentPath = parent.FullName;
                selectedIndex = 0;
                scrollOffset = 0;
                RefreshItems();
            }
        }

        public void DisplayError(string message)
        {
            ui.DisplayError(message);
        }
    }
}

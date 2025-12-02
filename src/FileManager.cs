using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using demonfm.UI;
using demonfm.Preview;
using demonfm.ConfigManager;
using demonfm.fzf;
using demonfm.zipper;
using demonfm.Lua;

namespace demonfm.filemanager
{
    public class FileManager
    {
        public static string? currentPath;
        public static List<FileSystemInfo>? items;
        public static int selectedIndex;
        public static int scrollOffset;
        public static bool running = true;

        private List<string> _clipboardPaths = new List<string>();
        private HashSet<string> _selectedFiles = new HashSet<string>();
        private bool _clipboardIsCut;

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
            Luaize.LoadTheme();
            config = Config.Load();
            currentPath = Directory.GetCurrentDirectory();
            selectedIndex = 0;
            scrollOffset = 0;
            _selectedFiles.Clear();
            
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

            ui.Draw(currentPath, items, selectedIndex, scrollOffset, preview.Lines, preview.ImagePath, _selectedFiles);
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
                case ConsoleKey.Spacebar:
                    ToggleSelection();
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
                case ConsoleKey.OemPeriod: // '.'
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
                case ConsoleKey.Y:
                    YankItem();
                    break;
                case ConsoleKey.X:
                    CutItem();
                    break;
                case ConsoleKey.P:
                    PasteItem();
                    break;
                case ConsoleKey.E:
                    HandleExtract();
                    break;
                case ConsoleKey.C:
                    HandleCompress();
                    break;
                case ConsoleKey.Z:
                    HandleFzf();
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
            
            List<string> pathsToDelete = new List<string>();
            if (_selectedFiles.Count > 0)
            {
                pathsToDelete.AddRange(_selectedFiles);
            }
            else
            {
                pathsToDelete.Add(items[selectedIndex].FullName);
            }

            bool confirm = ui.GetConfirmation($"Delete {pathsToDelete.Count} item(s)?");
            if (!confirm) return;

            foreach (var path in pathsToDelete)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    ui.DisplayError($"Delete failed for '{Path.GetFileName(path)}': {ex.Message}");
                }
            }
            
            _selectedFiles.Clear();
            if (selectedIndex >= items.Count - 1 && selectedIndex > 0) selectedIndex--;
            RefreshItems();
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
                    _selectedFiles.Clear();
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
                        RestoreState();
                    }
                    catch (Exception ex)
                    {
                        RestoreState();
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

        private void RestoreState()
        {
            Console.CursorVisible = false;
            Console.Title = "DemonFM";
            Console.Clear();
            RefreshItems();
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
                _selectedFiles.Clear();
                RefreshItems();
            }
        }

        public void DisplayError(string message)
        {
            ui.DisplayError(message);
        }

        private void ToggleSelection()
        {
            if (items != null && selectedIndex < items.Count)
            {
                var selected = items[selectedIndex].FullName;
                if (_selectedFiles.Contains(selected))
                    _selectedFiles.Remove(selected);
                else
                    _selectedFiles.Add(selected);

                if (selectedIndex < items.Count - 1)
                {
                    selectedIndex++;
                    int listHeight = ui.GetListHeight();
                    if (selectedIndex >= scrollOffset + listHeight)
                        scrollOffset++;
                }
            }
        }

        private void YankItem()
        {
            if (items == null) return;

            _clipboardPaths.Clear();
            
            if (_selectedFiles.Count > 0)
            {
                _clipboardPaths.AddRange(_selectedFiles);
                _selectedFiles.Clear();
            }
            else if (selectedIndex < items.Count)
            {
                _clipboardPaths.Add(items[selectedIndex].FullName);
            }
            
            _clipboardIsCut = false;
        }

        private void CutItem()
        {
            if (items == null) return;

            _clipboardPaths.Clear();

            if (_selectedFiles.Count > 0)
            {
                _clipboardPaths.AddRange(_selectedFiles);
                _selectedFiles.Clear();
            }
            else if (selectedIndex < items.Count)
            {
                _clipboardPaths.Add(items[selectedIndex].FullName);
            }

            _clipboardIsCut = true;
        }

        private void PasteItem()
        {
            if (_clipboardPaths.Count == 0 || currentPath == null) return;

            foreach (var sourcePath in _clipboardPaths.ToList())
            {
                bool isDir = Directory.Exists(sourcePath);
                bool isFile = File.Exists(sourcePath);

                if (!isDir && !isFile) 
                {
                    ui.DisplayError($"Source '{Path.GetFileName(sourcePath)}' not found.");
                    continue;
                }

                string name = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(currentPath, name);

                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    ui.DisplayError($"Destination '{name}' already exists.");
                    continue;
                }

                try
                {
                    if (_clipboardIsCut)
                    {
                        if (isDir) Directory.Move(sourcePath, destPath);
                        else File.Move(sourcePath, destPath);
                    }
                    else
                    {
                        if (isDir) CopyDirectory(sourcePath, destPath);
                        else File.Copy(sourcePath, destPath);
                    }
                }
                catch (Exception ex)
                {
                    ui.DisplayError($"Paste failed for '{name}': {ex.Message}");
                }
            }

            if (_clipboardIsCut)
            {
                 _clipboardPaths.Clear();
            }
            RefreshItems();
        }

        private void HandleExtract()
        {
            if (items == null || selectedIndex >= items.Count) return;
            
            var selected = items[selectedIndex];
            if (selected is DirectoryInfo) return; // Can't extract a directory

            // Prompt for destination name/folder
            // Default to a folder named after the file (without extension)
            string nameWithoutExt = Path.GetFileNameWithoutExtension(selected.Name);
            if (nameWithoutExt.EndsWith(".tar")) nameWithoutExt = Path.GetFileNameWithoutExtension(nameWithoutExt); // Handle .tar.gz -> .tar -> name

            string? input = ui.ReadInput($"Extract to (default: {nameWithoutExt}/): ");
            string folderName = string.IsNullOrWhiteSpace(input) ? nameWithoutExt : input;
            
            string destinationDir = Path.Combine(currentPath!, folderName);

            try
            {
                Console.Clear();
                Zipper.Extract(selected.FullName, destinationDir);
                RestoreState();
                RefreshItems();
            }
            catch (Exception ex)
            {
                RestoreState();
                ui.DisplayError($"Extraction failed: {ex.Message}");
            }
        }

        private void HandleCompress()
        {
            if (currentPath == null) return;
            
            List<string> sources = new List<string>();
            if (_selectedFiles.Count > 0)
            {
                sources.AddRange(_selectedFiles);
            }
            else if (items != null && selectedIndex < items.Count)
            {
                sources.Add(items[selectedIndex].FullName);
            }

            if (sources.Count == 0) return;

            string? archiveName = ui.ReadInput("Archive name (e.g. data.zip, backup.tar.gz): ");
            if (string.IsNullOrWhiteSpace(archiveName)) return;

            string destinationPath = Path.Combine(currentPath, archiveName);

            try
            {
                Console.Clear();
                Zipper.Compress(sources, destinationPath);
                RestoreState();
                RefreshItems();
            }
            catch (Exception ex)
            {
                RestoreState();
                ui.DisplayError($"Compression failed: {ex.Message}");
            }
        }

                private void HandleFzf()
                {
                    if (currentPath == null) return;
                    
                    if (!demonfm.fzf.FzfSelector.CheckFzfInstalled())
                    {
                        ui.DisplayError("fzf is not installed");
                        return;
                    }
        
                    Console.SetCursorPosition(0, 0);
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string? result = demonfm.fzf.FzfSelector.RunFzf(homeDirectory);
                    RestoreState();
        
                    if (string.IsNullOrEmpty(result)) return;
        
                    string fullPath = Path.GetFullPath(Path.Combine(homeDirectory, result));
        
                    if (Directory.Exists(fullPath))
                    {
                        currentPath = fullPath;
                        selectedIndex = 0;
                        scrollOffset = 0;
                        _selectedFiles.Clear();
                        RefreshItems();
                    }
                    else if (File.Exists(fullPath))
                    {
                        OpenFile(new FileInfo(fullPath));
                    }
                }
        private void OpenFile(FileInfo file)
        {
            if (!IsBinary(file))
            {
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
                    RestoreState();
                }
                catch (Exception ex)
                {
                    RestoreState();
                    ui.DisplayError($"Could not open editor: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file.FullName,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ui.DisplayError($"Could not open file: {ex.Message}");
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}

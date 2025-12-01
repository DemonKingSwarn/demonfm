using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace demonfm.filemanager
{
    public class FileManager
    {
        public static string? currentPath;
        public static List<FileSystemInfo>? items;
        public static int selectedIndex;
        public static int scrollOffset;
        public static bool running = true;

        public static int height;
        public static int width;
        public static int HeaderHeight = 2;
        public static int FooterHeight = 2;

        public void Initialize()
        {
            currentPath = Directory.GetCurrentDirectory();
            selectedIndex = 0;
            scrollOffset = 0;
            Console.CursorVisible = false;
            Console.Title = "DemonFM";
            RefreshItems();
        }

        public void RefreshItems()
        {
            if (currentPath == null) return;
            var dirInfo = new DirectoryInfo(currentPath);
            items = new List<FileSystemInfo>();

            try
            {
                items.AddRange(dirInfo.GetDirectories().OrderBy(d => d.Name));
                items.AddRange(dirInfo.GetFiles().OrderBy(f => f.Name));
            }
            catch (UnauthorizedAccessException)
            {
                DisplayError("Access Denied, press any key to return.");
                NavigateUp();
                return;
            }

            if (selectedIndex >= items.Count && items.Count > 0) selectedIndex = items.Count - 1;
            else if (items.Count == 0) selectedIndex = 0;
        }

        public void Draw()
        {
            height = Console.WindowHeight;
            width = Console.WindowWidth;
            int listHeight = height - HeaderHeight - FooterHeight;

            Console.SetCursorPosition(0, 0);

            DrawHeader();

            for (int i = 0; i < listHeight; i++)
            {
                int itemIndex = i + scrollOffset;
                if (items != null && itemIndex < items.Count)
                {
                    var item = items[itemIndex];
                    bool isSelected = itemIndex == selectedIndex;

                    if (isSelected)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                    }
                    else
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.ForegroundColor = item is DirectoryInfo ? ConsoleColor.Blue : ConsoleColor.White;
                    }

                    string typeIcon = item is DirectoryInfo ? "[DIR] " : "      ";
                    string size = item is FileInfo f ? FormatBytes(f.Length) : "";

                    int maxNameLen = width - 25;
                    string name = item.Name.Length > maxNameLen ? item.Name.Substring(0, maxNameLen - 3) + "..." : item.Name;

                    string line = $"{typeIcon}{name}".PadRight(width - 15) + size.PadLeft(15);

                    if (line.Length < width) line = line.PadRight(width);
                    if (line.Length > width) line = line.Substring(0, width);

                    Console.Write(line);
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.Write(new string(' ', width));
                }

                Console.ResetColor();
            }

            DrawFooter();
        }

        public void DrawHeader()
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            string pathDisplay = $" Path: {currentPath} ";
            Console.Write(pathDisplay.PadRight(width));

            string columns = " Name".PadRight(width - 15) + "Size/Date ".PadLeft(15);
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Write(columns.PadRight(width));
            Console.ResetColor();
        }

        void DrawFooter()
        {
            Console.SetCursorPosition(0, height - 1);
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.Yellow;
            string controls = " [UP/DOWN] Nav  [ENTER] Open  [BACKSPACE] Parent  [Q] Quit ";
            Console.Write(controls.PadRight(width));
            Console.ResetColor();
        }

        public void HandleInput()
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        if (selectedIndex < scrollOffset)
                            scrollOffset--;
                    }
                    break;
                case ConsoleKey.DownArrow:
                    if (items != null && selectedIndex < items.Count - 1)
                    {
                        selectedIndex++;
                        int listHeight = height - HeaderHeight - FooterHeight;
                        if (selectedIndex >= scrollOffset + listHeight)
                            scrollOffset++;
                    }
                    break;
                case ConsoleKey.Enter:
                    OpenSelectedItem();
                    break;
                case ConsoleKey.Backspace:
                case ConsoleKey.LeftArrow:
                    NavigateUp();
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
                        int h = height - HeaderHeight - FooterHeight;
                        scrollOffset = Math.Max(0, items.Count - h);
                    }
                    break;
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
                    DisplayError($"Cannot access {selected.Name}: Access Denied");
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
                    DisplayError($"Could not open file: {ex.Message}");
                }
            }
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
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            int x = (width - message.Length) / 2;
            int y = height / 2;
            Console.SetCursorPosition(x > 0 ? x : 0, y);
            Console.Write($" {message} ");
            Console.ReadKey(true);
            Console.ResetColor();
        }

        public string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }
    }
}
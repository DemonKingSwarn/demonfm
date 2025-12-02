using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using demonfm.Lua;

namespace demonfm.UI
{
    public class Ui
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public const int HeaderHeight = 4; 
        public const int FooterHeight = 2;

        public Ui()
        {
            UpdateDimensions();
        }

        public void UpdateDimensions()
        {
            Width = Console.WindowWidth;
            Height = Console.WindowHeight;
        }

        public int GetListHeight()
        {
            return Height - HeaderHeight - FooterHeight;
        }

        public void Draw(string? currentPath, List<FileSystemInfo>? items, int selectedIndex, int scrollOffset, List<string> previewLines, string? previewImagePath, HashSet<string> selectedFiles)
        {
            UpdateDimensions();
            ClearPreviewImage();

            Console.SetCursorPosition(0, 0);
            
            Console.ResetColor();

            int midX = Width / 2;

            DrawHeader(currentPath, midX);
            DrawList(items, selectedIndex, scrollOffset, midX, selectedFiles);
            DrawPreview(previewLines, midX, previewImagePath);
            DrawFooter(items, selectedIndex, midX);
        }

        private void DrawHeader(string? currentPath, int midX)
        {
            Console.SetCursorPosition(0, 0);
            SetColor(Luaize.GetColor("HeaderPath"), true);
            Console.Write(" " + (currentPath ?? "/"));
            Console.ResetColor();
            ClearLineRemainder();

            Console.SetCursorPosition(0, 1);
            // ┌───┬───┐
            string leftBorder = new string('─', Math.Max(0, midX - 1));
            string rightBorder = new string('─', Math.Max(0, Width - midX - 2));
            Console.Write("┌" + leftBorder + "┬" + rightBorder + "┐");

            Console.SetCursorPosition(0, 2);
            Console.Write("│");
            SetColor(Luaize.GetColor("HeaderTitle"), true);
            
            string header = string.Format(" {0,-20} {1}", "Date", "Name");
            PrintContent(header, midX - 1);
            
            Console.ResetColor();
            Console.SetCursorPosition(midX, 2);
            Console.Write("│");
            
            Console.SetCursorPosition(Width - 1, 2);
            Console.Write("│");

            Console.SetCursorPosition(0, 3);
            Console.Write("├" + leftBorder + "┼" + rightBorder + "┤");
        }

        private void DrawList(List<FileSystemInfo>? items, int selectedIndex, int scrollOffset, int midX, HashSet<string> selectedFiles)
        {
            int listHeight = GetListHeight();
            int contentWidth = midX - 1;

            for (int i = 0; i < listHeight; i++)
            {
                int y = HeaderHeight + i;
                Console.SetCursorPosition(0, y);
                Console.Write("│"); // Left Border
                
                int itemIndex = i + scrollOffset;
                if (items != null && itemIndex < items.Count)
                {
                    var item = items[itemIndex];
                    bool isSelected = itemIndex == selectedIndex;
                    bool isMultiSelected = selectedFiles.Contains(item.FullName);

                    if (isSelected)
                    {
                        SetColor(Luaize.GetColor("ListSelectedBg"), false);
                        if (isMultiSelected) SetColor(Luaize.GetColor("ListMultiSelectedFg"), true);
                        else SetColor(Luaize.GetColor("ListSelectedFg"), true);
                    }
                    else
                    {
                        Console.ResetColor();
                        if (isMultiSelected) 
                        {
                            SetColor(Luaize.GetColor("ListMultiSelectedFg"), true);
                        }
                        else if (item is DirectoryInfo) 
                        {
                            SetColor(Luaize.GetColor("ListDirectory"), true);
                        }
                        else if (item.Extension == ".exe" || item.Extension == ".sh") 
                        {
                            SetColor(Luaize.GetColor("ListExecutable"), true);
                        }
                        else 
                        {
                            SetColor(Luaize.GetColor("ListDefault"), true);
                        }
                    }

                    string date = item.LastWriteTime.ToString("MMM dd HH:mm");
                    string name = item.Name;

                    string line = string.Format(" {0,-20} {1}", date, name);
                    
                    PrintContent(line, contentWidth);
                }
                else
                {
                    Console.ResetColor();
                    Console.Write(new string(' ', Math.Max(0, contentWidth)));
                }
                
                Console.ResetColor(); 
                Console.SetCursorPosition(midX, y);
                Console.Write("│");
            }
        }

        private void DrawPreview(List<string> previewLines, int midX, string? previewRawData)
        {
            int listHeight = GetListHeight();
            int startX = midX + 1;
            int contentWidth = Width - startX - 1;

            // Draw borders and clear area (or print text preview if no raw data)
            for (int i = 0; i < listHeight; i++)
            {
                int y = HeaderHeight + i;
                Console.SetCursorPosition(startX, y);
                
                if (previewRawData == null && i < previewLines.Count)
                {
                    string line = previewLines[i];
                    PrintContent(line, contentWidth);
                }
                else
                {
                    Console.Write(new string(' ', Math.Max(0, contentWidth)));
                }

                Console.SetCursorPosition(Width - 1, y);
                Console.Write("│");
            }

            if (previewRawData != null)
            {
                // Check for Graphics (Sixel starts with ESC P, Kitty with ESC _ G)
                bool isGraphics = previewRawData.StartsWith("\x1bP") || previewRawData.StartsWith("\x1b_G");

                if (isGraphics)
                {
                    Console.SetCursorPosition(startX, HeaderHeight);
                    Console.Write(previewRawData);
                }
                else
                {
                    // Text-based chafa output (symbols)
                    // Print line by line to maintain cursor X position
                    var lines = previewRawData.Split('\n');
                    for (int i = 0; i < listHeight && i < lines.Length; i++)
                    {
                        Console.SetCursorPosition(startX, HeaderHeight + i);
                        // Don't truncate ansi here, trust chafa -s
                        Console.Write(lines[i]);
                    }
                }
            }
        }

        private void ClearPreviewImage()
        {
            Console.Write("\x1b_Ga=d,d=i,i=1\x1b\\");
        }

        private void DrawImagePreview(string path, int x, int y, int w, int h)
        {
            try
            {
                string base64Path = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(path));
                Console.SetCursorPosition(x, y);
                Console.Write($"\x1b_Ga=T,t=f,i=1,z=0,C=1,c={w},r={h};{base64Path}\x1b\\");
            }
            catch 
            {
                // If encoding fails or path is bad, do nothing
            }
        }

        private void DrawFooter(List<FileSystemInfo>? items, int selectedIndex, int midX)
        {
            Console.SetCursorPosition(0, Height - 2);
            string leftBorder = new string('─', Math.Max(0, midX - 1));
            string rightBorder = new string('─', Math.Max(0, Width - midX - 2));
            Console.Write("└" + leftBorder + "┴" + rightBorder + "┘");

            Console.SetCursorPosition(0, Height - 1);
            Console.ResetColor();
            
            string status = "";
            if (items != null && selectedIndex >= 0 && selectedIndex < items.Count)
            {
                var item = items[selectedIndex];
                string sizeInfo = item is FileInfo f ? $" {FormatBytes(f.Length)}" : "";
                status = $"{selectedIndex + 1}/{items.Count} : {item.Name}{sizeInfo}";
            }
            else
            {
                status = "0/0";
            }

            string controls = "[r]ename [d]elete [a]dd [y]ank [x]cut [p]aste [e]xtract [c]ompress [space]sel [q]uit";
            
            Console.Write(" " + status);
            
            int currentPos = Console.CursorLeft;
            int pad = Width - currentPos - controls.Length - 3; 
            if (pad > 0) Console.Write(new string(' ', pad));
            
            SetColor(Luaize.GetColor("Footer"), true);
            Console.Write(" " + controls + " ");
            Console.ResetColor();
        }

        public string? ReadInput(string prompt)
        {
            Console.SetCursorPosition(0, Height - 1);
            Console.ResetColor();
            ClearLineRemainder();
            Console.SetCursorPosition(0, Height - 1);
            Console.Write(prompt);
            
            Console.CursorVisible = true;
            string? input = Console.ReadLine();
            Console.CursorVisible = false;
            return input;
        }

        public bool GetConfirmation(string message)
        {
            Console.SetCursorPosition(0, Height - 1);
            Console.ResetColor();
            ClearLineRemainder();
            Console.SetCursorPosition(0, Height - 1);
            
            SetColor(Luaize.GetColor("Confirmation"), true);
            Console.Write($"{message} (y/N) ");
            Console.ResetColor();

            var key = Console.ReadKey(true);
            return key.Key == ConsoleKey.Y;
        }

        private void PrintContent(string content, int width)
        {
            if (width <= 0) return;
            
            string truncated = TruncateAnsi(content, width);
            int visibleLen = GetVisibleLength(truncated);
            
            Console.Write(truncated);
            if (visibleLen < width)
            {
                Console.Write(new string(' ', width - visibleLen));
            }
        }

        private string TruncateAnsi(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (GetVisibleLength(text) <= width) return text;

            var sb = new System.Text.StringBuilder();
            int visibleCount = 0;
            bool inAnsi = false;
            
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\x1b')
                {
                    inAnsi = true;
                    sb.Append(c);
                }
                else if (inAnsi)
                {
                    sb.Append(c);
                    if (c == 'm') inAnsi = false;
                }
                else
                {
                    if (visibleCount >= width) break;
                    sb.Append(c);
                    visibleCount++;
                }
            }
            
            // Reset color at end just in case
            sb.Append("\x1b[0m");
            return sb.ToString();
        }

        private int GetVisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Remove ANSI codes: \x1b\[[0-9;]*m
            // Simplified regex for standard colors
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", "").Length;
        }

        private void ClearLineRemainder()
        {
            int currentLeft = Console.CursorLeft;
            int remaining = Width - currentLeft;
            if (remaining > 0) Console.Write(new string(' ', remaining));
        }

        public void DisplayError(string message)
        {
            Console.SetCursorPosition(0, Height - 1);
            SetColor(Luaize.GetColor("ErrorBg"), false);
            SetColor(Luaize.GetColor("ErrorFg"), true);
            Console.Write($" error: {message} ".PadRight(Width));
            Console.ReadKey(true);
            Console.ResetColor();
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "K", "M", "G", "T" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }

        private void SetColor(DemonColor color, bool isForeground)
        {
            if (color.IsRgb)
            {
                string seq = isForeground 
                    ? $"\x1b[38;2;{color.R};{color.G};{color.B}m"
                    : $"\x1b[48;2;{color.R};{color.G};{color.B}m";
                Console.Write(seq);
            }
            else
            {
                if (isForeground) Console.ForegroundColor = color.Standard;
                else Console.BackgroundColor = color.Standard;
            }
        }    }
}

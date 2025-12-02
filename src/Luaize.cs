using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using NLua;

namespace demonfm.Lua
{
    public struct DemonColor
    {
        public bool IsRgb;
        public ConsoleColor Standard;
        public byte R;
        public byte G;
        public byte B;

        public static DemonColor FromConsole(ConsoleColor c) => new DemonColor { IsRgb = false, Standard = c };
        
        public static DemonColor FromHex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, null, out int rgb))
            {
                return new DemonColor 
                { 
                    IsRgb = true, 
                    R = (byte)((rgb >> 16) & 0xFF), 
                    G = (byte)((rgb >> 8) & 0xFF), 
                    B = (byte)(rgb & 0xFF) 
                };
            }
            return FromConsole(ConsoleColor.White); // Fallback
        }
    }

    public class Luaize
    {
        public static Dictionary<string, DemonColor> Theme = new Dictionary<string, DemonColor>();

        public static void LoadTheme()
        {
            // Defaults
            Theme["HeaderPath"] = DemonColor.FromConsole(ConsoleColor.Magenta);
            Theme["HeaderTitle"] = DemonColor.FromConsole(ConsoleColor.DarkGray);
            Theme["Border"] = DemonColor.FromConsole(ConsoleColor.White);
            Theme["ListSelectedBg"] = DemonColor.FromConsole(ConsoleColor.DarkGray);
            Theme["ListSelectedFg"] = DemonColor.FromConsole(ConsoleColor.White);
            Theme["ListMultiSelectedFg"] = DemonColor.FromConsole(ConsoleColor.Yellow);
            Theme["ListDirectory"] = DemonColor.FromConsole(ConsoleColor.Blue);
            Theme["ListExecutable"] = DemonColor.FromConsole(ConsoleColor.Green);
            Theme["ListDefault"] = DemonColor.FromConsole(ConsoleColor.Gray);
            Theme["Footer"] = DemonColor.FromConsole(ConsoleColor.DarkGray);
            Theme["ErrorBg"] = DemonColor.FromConsole(ConsoleColor.Red);
            Theme["ErrorFg"] = DemonColor.FromConsole(ConsoleColor.White);
            Theme["Confirmation"] = DemonColor.FromConsole(ConsoleColor.Yellow);

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string scriptPath = Path.Combine(home, ".config", "demonfm", "scripts", "colors.lua");

            if (File.Exists(scriptPath))
            {
                try
                {
                    using (var lua = new NLua.Lua())
                    {
                        lua.DoFile(scriptPath);
                        var colors = lua["Colors"] as LuaTable;
                        if (colors != null)
                        {
                            foreach (var key in colors.Keys)
                            {
                                string k = key.ToString();
                                var val = colors[key];
                                if (val != null)
                                {
                                    string sVal = val.ToString();
                                    if (sVal.StartsWith("#"))
                                    {
                                        if (Theme.ContainsKey(k)) Theme[k] = DemonColor.FromHex(sVal);
                                    }
                                    else
                                    {
                                        if (Enum.TryParse(sVal, true, out ConsoleColor c))
                                        {
                                            if (Theme.ContainsKey(k)) Theme[k] = DemonColor.FromConsole(c);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public static DemonColor GetColor(string key)
        {
            if (Theme.TryGetValue(key, out DemonColor val))
            {
                return val;
            }
            return DemonColor.FromConsole(ConsoleColor.White);
        }
    }
}

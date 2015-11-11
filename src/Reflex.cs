using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;


namespace Uber.MmeMuxer
{
    public class VdfNode
    {
        public VdfNode(VdfNode parent)
        {
            Parent = parent;
        }

        public VdfNode(VdfNode parent, string name)
        {
            Parent = parent;
            Name = name;
        }

        public VdfNode FindChild(string name)
        {
            return Children.Find(c => c.Name == name);
        }

        public VdfNode FindChild(params string[] names)
        {
            var node = this;
            foreach(var name in names)
            {
                node = node.FindChild(name);
                if(node == null)
                {
                    return null;
                }
            }

            return node;
        }

        public string GetUnescapedValue()
        {
            return Value.Replace(@"\\", @"\");
        }

        public string TryGetChildValue(string name, string errorValue)
        {
            var child = FindChild(name);
            if(child == null)
            {
                return errorValue;
            }

            return child.GetUnescapedValue();
        }

        public VdfNode Parent;
        public string Name = "";
        public string Value = "";
        public readonly List<VdfNode> Children = new List<VdfNode>();
        public bool IsValue = false;
    }

    public class VdfParser
    {
        public static VdfNode ParseVdfFile(string filePath)
        {
            return ParseVdfString(File.ReadAllText(filePath));
        }

        public static VdfNode ParseVdfString(string vdfString)
        {
            vdfString = CleanUpInput(vdfString);

            var rootNode = new VdfNode(null, "Root");
            var currentNode = new VdfNode(rootNode);
            bool readName = false;
            var i = 0;
            while(i < vdfString.Length)
            {
                var c = vdfString[i];
                if(c == '"')
                {
                    var endQuoteIdx = vdfString.IndexOf('"', i + 1);
                    var name = vdfString.Substring(i + 1, endQuoteIdx - i - 1);
                    if(readName)
                    {
                        currentNode.Value = name;
                        currentNode.IsValue = true;
                        currentNode = new VdfNode(currentNode.Parent);
                    }
                    else
                    {
                        currentNode.Name = name;
                        currentNode.Parent.Children.Add(currentNode);
                    }
                    readName = !readName;
                    i = endQuoteIdx + 1;
                }
                else if(c == '{')
                {
                    currentNode = new VdfNode(currentNode, currentNode.Name);
                    readName = false;
                    ++i;
                }
                else if(c == '}')
                {
                    if(currentNode.Parent == null)
                    {
                        break;
                    }
                    currentNode = new VdfNode(currentNode.Parent.Parent);
                    readName = false;
                    ++i;
                }
            }

            return rootNode;
        }

        private static string CleanUpInput(string input)
        {
            var inputLength = input.Length;
            var outputLength = 0;
            var output = new char[inputLength];
            for(int i = 0; i < inputLength; ++i)
            {
                char c = input[i];
                if(char.IsWhiteSpace(c))
                {
                    continue;
                }

                output[outputLength] = c;
                ++outputLength;
            }

            return new string(output, 0, outputLength);
        }
    }

    public static class Reflex
    {
        public static string GetReflexFolder()
        {
            var steamBasePath = GetSteamBasePath();
            if(steamBasePath == null)
            {
                return null;
            }

            var reflexPath = Path.Combine(steamBasePath, @"SteamApps\common\Reflex");
            if(Directory.Exists(reflexPath))
            {
                return reflexPath;
            }

            var extraPaths = GetSteamExtraPaths(steamBasePath);
            foreach(var extraPath in extraPaths)
            {
                reflexPath = Path.Combine(extraPath, @"SteamApps\common\Reflex");
                if(Directory.Exists(reflexPath))
                {
                    return reflexPath;
                }
            }

            return null;
        }

        public static string GetReflexReplaysFolder()
        {
            var reflexRoot = GetReflexFolder();
            if(reflexRoot == null)
            {
                return null;
            }

            var replaysPath = Path.Combine(reflexRoot, @"base\replays");
            if(!Directory.Exists(replaysPath))
            {
                return null;
            }

            return replaysPath;
        }

        private static string GetSteamBasePath()
        {
            var steamRegKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if(steamRegKey == null)
            {
                return null;
            }

            var steamBasePath = steamRegKey.GetValue("SteamPath", "").ToString();
            steamBasePath = Path.GetFullPath(steamBasePath);
            if(!Directory.Exists(steamBasePath))
            {
                return null;
            }

            return steamBasePath;
        }

        private static List<string> GetSteamExtraPaths(string steamBasePath)
        {
            var extraPaths = new List<string>();
            var config = VdfParser.ParseVdfFile(Path.Combine(steamBasePath, @"config\config.vdf"));
            var steamNode = config.FindChild("InstallConfigStore", "Software", "Valve", "Steam");
            if(steamNode != null)
            {
                for(var i = 1; ; ++i)
                {
                    var installNode = steamNode.FindChild("BaseInstallFolder_" + i.ToString());
                    if(installNode == null)
                    {
                        break;
                    }

                    extraPaths.Add(Path.GetFullPath(installNode.GetUnescapedValue()));
                }
            }

            return extraPaths;
        }
    }
}
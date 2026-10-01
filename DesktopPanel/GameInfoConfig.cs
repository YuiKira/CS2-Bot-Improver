using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CS2BotTools;

internal static class GameInfoConfig
{
    private static readonly string[] BotPaths = { "csgo/overrides/botprofile.vpk", "csgo/addons/metamod" };

    private sealed class Token
    {
        public string Value = string.Empty;
        public int Start;
        public int End;
    }

    // Token positions let us change SearchPaths without rewriting Valve's other settings.
    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        for (int i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i]) || text[i] == '\uFEFF') { i++; continue; }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            int start = i;
            string value;
            if (text[i] == '"')
            {
                int valueStart = ++i;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++;
                    i++;
                }
                if (i == text.Length) throw new InvalidDataException("Unterminated gameinfo string");
                value = text.Substring(valueStart, i - valueStart);
                i++;
            }
            else if (text[i] == '{' || text[i] == '}') value = text[i++].ToString();
            else
            {
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}') i++;
                value = text.Substring(start, i - start);
            }
            tokens.Add(new Token { Value = value, Start = start, End = i });
        }
        return tokens;
    }

    private static int SearchPathsOpening(List<Token> tokens)
    {
        var openings = new List<int>();
        var stack = new Stack<string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Value == "{")
            {
                string name = i > 0 ? tokens[i - 1].Value : string.Empty;
                if (name.Equals("SearchPaths", StringComparison.OrdinalIgnoreCase)
                    && stack.Count > 0 && stack.Peek().Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
                    openings.Add(i);
                stack.Push(name);
            }
            else if (tokens[i].Value == "}")
            {
                if (stack.Count == 0) throw new InvalidDataException("Unbalanced gameinfo blocks");
                stack.Pop();
            }
        }
        if (stack.Count != 0 || openings.Count != 1)
            throw new InvalidDataException("Expected one FileSystem/SearchPaths block");
        return openings[0];
    }

    public static string SetBotMode(string text, bool enabled)
    {
        var tokens = Tokenize(text);
        int opening = SearchPathsOpening(tokens);
        var removals = new List<Tuple<int, int>>();
        for (int i = opening + 1; i < tokens.Count && tokens[i].Value != "}"; i += 2)
        {
            if (i + 1 >= tokens.Count || tokens[i].Value == "{" || tokens[i + 1].Value == "}")
                throw new InvalidDataException("Invalid SearchPaths entry");
            if (tokens[i].Value.Equals("Game", StringComparison.OrdinalIgnoreCase)
                && BotPaths.Contains(tokens[i + 1].Value.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase))
            {
                int start = tokens[i].Start;
                int end = tokens[i + 1].End;
                int lineStart = text.LastIndexOf('\n', start) + 1;
                int nextLine = text.IndexOf('\n', end);
                int lineEnd = nextLine < 0 ? text.Length : nextLine;
                string suffix = text.Substring(end, lineEnd - end).Trim();
                if (text.Substring(lineStart, start - lineStart).Trim().Length == 0
                    && (suffix.Length == 0 || suffix.StartsWith("//", StringComparison.Ordinal)))
                {
                    start = lineStart;
                    end = nextLine < 0 ? lineEnd : nextLine + 1;
                }
                removals.Add(Tuple.Create(start, end));
            }
        }
        foreach (var span in removals.OrderByDescending(span => span.Item1))
            text = text.Remove(span.Item1, span.Item2 - span.Item1);
        if (!enabled) return text;
        tokens = Tokenize(text);
        opening = SearchPathsOpening(tokens);
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        int insertion = tokens[opening].End;
        string trailing = insertion < text.Length && (text[insertion] == '\r' || text[insertion] == '\n')
            ? string.Empty : newline;
        return text.Insert(insertion, newline + "\t\t\tGame\t" + BotPaths[0]
            + newline + "\t\t\tGame\t" + BotPaths[1] + trailing);
    }

    public static void ValidateLayers(string text, string csgoRoot)
    {
        var tokens = Tokenize(text);
        for (int i = 0; i + 1 < tokens.Count; i++)
        {
            if (!tokens[i].Value.Equals("LayeredOnMod", StringComparison.OrdinalIgnoreCase)) continue;
            var gameRoot = Directory.GetParent(Path.GetFullPath(csgoRoot))?.FullName
                ?? throw new InvalidDataException("Invalid CS2 game directory");
            var layeredInfo = Path.Combine(gameRoot, tokens[i + 1].Value, "gameinfo.gi");
            if (!File.Exists(layeredInfo))
                throw new InvalidDataException("gameinfo.gi 引用了不存在的游戏目录。请先在 Steam 验证游戏文件完整性，再启用 Bot 模式。");
        }
    }
}

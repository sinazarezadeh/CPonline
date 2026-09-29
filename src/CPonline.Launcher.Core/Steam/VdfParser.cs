using System.Text;

namespace CPonline.Launcher.Core.Steam;

/// <summary>
/// Minimal recursive-descent parser for Valve's KeyValues ("VDF") text format, as used by
/// Steam's steamapps/libraryfolders.vdf and appmanifest_*.acf files. Pure text in, tree out -
/// no file or registry access - so it's fully unit-testable on any OS.
/// </summary>
public static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        var tokens = Tokenize(text);
        var pos = 0;
        var root = new VdfNode();
        while (pos < tokens.Count)
        {
            var key = ExpectString(tokens, ref pos);
            var child = ParseValueOrBlock(tokens, ref pos);
            root.Children.Add((key, child));
        }

        return root;
    }

    private static VdfNode ParseValueOrBlock(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
        {
            throw new FormatException("Unexpected end of VDF input.");
        }

        var tok = tokens[pos];
        if (tok.Type == TokenType.OpenBrace)
        {
            pos++;
            var node = new VdfNode();
            while (pos < tokens.Count && tokens[pos].Type != TokenType.CloseBrace)
            {
                var key = ExpectString(tokens, ref pos);
                var child = ParseValueOrBlock(tokens, ref pos);
                node.Children.Add((key, child));
            }

            if (pos >= tokens.Count)
            {
                throw new FormatException("Unterminated block in VDF input.");
            }

            pos++; // consume the closing brace
            return node;
        }

        if (tok.Type == TokenType.String)
        {
            pos++;
            return new VdfNode { Value = tok.Text };
        }

        throw new FormatException($"Unexpected token '{tok.Text}' at position {pos}.");
    }

    private static string ExpectString(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count || tokens[pos].Type != TokenType.String)
        {
            throw new FormatException("Expected a quoted key in VDF input.");
        }

        var text = tokens[pos].Text;
        pos++;
        return text;
    }

    private enum TokenType
    {
        String,
        OpenBrace,
        CloseBrace,
    }

    private readonly record struct Token(TokenType Type, string Text);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '{')
            {
                tokens.Add(new Token(TokenType.OpenBrace, "{"));
                i++;
                continue;
            }

            if (c == '}')
            {
                tokens.Add(new Token(TokenType.CloseBrace, "}"));
                i++;
                continue;
            }

            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        sb.Append(text[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(text[i]);
                        i++;
                    }
                }

                if (i >= text.Length)
                {
                    throw new FormatException("Unterminated quoted string in VDF input.");
                }

                i++; // consume the closing quote
                tokens.Add(new Token(TokenType.String, sb.ToString()));
                continue;
            }

            // Tolerate bare (unquoted) tokens - rare in Steam's own files, but valid VDF.
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}')
            {
                i++;
            }

            tokens.Add(new Token(TokenType.String, text[start..i]));
        }

        return tokens;
    }
}

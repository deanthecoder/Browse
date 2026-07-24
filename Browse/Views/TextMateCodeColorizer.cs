// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using AvaloniaFontStyle = Avalonia.Media.FontStyle;
using TextMateFontStyle = TextMateSharp.Themes.FontStyle;

namespace Browse.Views;

/// <summary>
/// Applies TextMate's Dark+ colors only while AvaloniaEdit renders visible source lines.
/// </summary>
/// <remarks>
/// Tokenization is performed once for the bounded preview, while visual elements are created only for the editor viewport.
/// </remarks>
internal sealed class TextMateCodeColorizer(IReadOnlyList<IReadOnlyList<TextMateCodeColorizer.StyledToken>> lines)
    : DocumentColorizingTransformer
{
    private static readonly RegistryOptions RegistryOptions = new(ThemeName.DarkPlus);
    private static readonly Registry Registry = new(RegistryOptions);
    private static readonly Theme Theme = Theme.CreateFromRawTheme(RegistryOptions.LoadTheme(ThemeName.DarkPlus), RegistryOptions);
    private static readonly Dictionary<string, IBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public static TextMateCodeColorizer Create(string path, string text)
    {
        var scope = RegistryOptions.GetScopeByExtension(Path.GetExtension(path));
        if (scope == null)
            return null;
        var grammar = Registry.LoadGrammar(scope);
        if (grammar == null)
            return null;

        var tokenizedLines = new List<IReadOnlyList<StyledToken>>();
        IStateStack ruleStack = null;
        foreach (var line in text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
            ruleStack = result.RuleStack;
            tokenizedLines.Add(result.Tokens
                .Select(token => CreateStyledToken(token.StartIndex, token.EndIndex, token.Scopes))
                .Where(token => token != null)
                .ToArray());
        }
        return new TextMateCodeColorizer(tokenizedLines);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.LineNumber < 1 || line.LineNumber > lines.Count)
            return;
        foreach (var token in lines[line.LineNumber - 1])
        {
            var startOffset = line.Offset + Math.Min(token.StartIndex, line.Length);
            var endOffset = line.Offset + Math.Min(token.EndIndex, line.Length);
            if (startOffset >= endOffset)
                continue;
            ChangeLinePart(startOffset, endOffset, visualLine => ApplyStyle(visualLine, token));
        }
    }

    private static StyledToken CreateStyledToken(int startIndex, int endIndex, IList<string> scopes)
    {
        var foreground = 0;
        var fontStyle = TextMateFontStyle.NotSet;
        foreach (var rule in Theme.Match(scopes))
        {
            if (foreground == 0 && rule.foreground > 0)
                foreground = rule.foreground;
            if (fontStyle == TextMateFontStyle.NotSet && rule.fontStyle > 0)
                fontStyle = rule.fontStyle;
        }
        if (foreground == 0 && fontStyle == TextMateFontStyle.NotSet)
            return null;
        return new StyledToken(startIndex, endIndex, GetBrush(foreground), fontStyle);
    }

    private static IBrush GetBrush(int colorId)
    {
        if (colorId <= 0)
            return null;
        var value = Theme.GetColor(colorId);
        if (string.IsNullOrWhiteSpace(value) || !Color.TryParse(value, out var color))
            return null;
        lock (BrushCache)
        {
            if (!BrushCache.TryGetValue(value, out var brush))
                BrushCache[value] = brush = new ImmutableSolidColorBrush(color);
            return brush;
        }
    }

    private static void ApplyStyle(VisualLineElement visualLine, StyledToken token)
    {
        if (token.Foreground != null)
            visualLine.TextRunProperties.SetForegroundBrush(token.Foreground);
        var hasFontStyle = token.FontStyle != TextMateFontStyle.NotSet;
        var style = hasFontStyle && token.FontStyle.HasFlag(TextMateFontStyle.Italic) ? AvaloniaFontStyle.Italic : AvaloniaFontStyle.Normal;
        var weight = hasFontStyle && token.FontStyle.HasFlag(TextMateFontStyle.Bold) ? FontWeight.Bold : FontWeight.Regular;
        if (style != visualLine.TextRunProperties.Typeface.Style || weight != visualLine.TextRunProperties.Typeface.Weight)
        {
            visualLine.TextRunProperties.SetTypeface(new Typeface(
                visualLine.TextRunProperties.Typeface.FontFamily,
                style,
                weight));
        }
        if (hasFontStyle && token.FontStyle.HasFlag(TextMateFontStyle.Underline))
            visualLine.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
    }

    internal sealed record StyledToken(int StartIndex, int EndIndex, IBrush Foreground, TextMateFontStyle FontStyle);
}

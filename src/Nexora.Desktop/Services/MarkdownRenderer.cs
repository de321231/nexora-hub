using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nexora.Desktop.Services;

/// <summary>
/// Konvertiert Markdown in ein <see cref="FlowDocument"/> für die Anzeige in
/// einem schreibgeschützten RichTextBox (KI-Chat-Ausgaben). Unterstützt
/// Überschriften, Listen, Code-Blöcke, Zitate, Trennlinien sowie fett/kursiv/
/// Code/Links im Fließtext.
/// </summary>
public static class MarkdownRenderer
{
    public static FlowDocument ToFlowDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            LineHeight = 22
        };

        if (string.IsNullOrWhiteSpace(markdown)) return doc;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inCodeBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();

            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                doc.Blocks.Add(new Paragraph(new Run(line)
                {
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 12.5,
                    Foreground = CodeForeground
                })
                {
                    Margin = new Thickness(14, 0, 6, 4),
                    Padding = new Thickness(4, 1, 4, 1)
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;

            // Überschrift
            var h = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (h.Success)
            {
                var level = h.Groups[1].Value.Length;
                var para = new Paragraph { Margin = new Thickness(0, level == 1 ? 10 : 6, 0, 4) };
                var run = BuildInline(h.Groups[2].Value);
                run.FontSize = level switch { 1 => 19, 2 => 16, _ => 14.5 };
                run.FontWeight = FontWeights.Bold;
                para.Inlines.Add(run);
                doc.Blocks.Add(para);
                continue;
            }

            // Trennlinie
            if (Regex.IsMatch(line.Trim(), @"^(-{3,}|\*{3,}|_{3,})$"))
            {
                doc.Blocks.Add(new Paragraph
                {
                    Margin = new Thickness(0, 6, 0, 6),
                    BorderBrush = MutedBorder,
                    BorderThickness = new Thickness(0, 0, 0, 1)
                });
                continue;
            }

            // Zitat
            if (line.TrimStart().StartsWith("> "))
            {
                var para = new Paragraph
                {
                    Margin = new Thickness(16, 2, 0, 6),
                    Padding = new Thickness(12, 6, 12, 6),
                    BorderBrush = AccentBrush,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Background = SurfaceBrush
                };
                para.Inlines.Add(BuildInline(line.TrimStart()[2..]));
                doc.Blocks.Add(para);
                continue;
            }

            // Ungeordnete Liste
            var ul = Regex.Match(line.TrimStart(), @"^([-*+])\s+(.*)$");
            if (ul.Success)
            {
                var item = new Paragraph { Margin = new Thickness(18, 1, 0, 3), TextIndent = -14 };
                item.Inlines.Add(new Run("•  ") { Foreground = AccentBrush });
                item.Inlines.Add(BuildInline(ul.Groups[2].Value));
                doc.Blocks.Add(item);
                continue;
            }

            // Geordnete Liste
            var ol = Regex.Match(line.TrimStart(), @"^(\d+)[.)]\s+(.*)$");
            if (ol.Success)
            {
                var item = new Paragraph { Margin = new Thickness(18, 1, 0, 3) };
                item.Inlines.Add(new Run($"{ol.Groups[1].Value}.  ") { Foreground = AccentLightBrush });
                item.Inlines.Add(BuildInline(ol.Groups[2].Value));
                doc.Blocks.Add(item);
                continue;
            }

            // Normaler Absatz
            var para2 = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
            para2.Inlines.Add(BuildInline(line));
            doc.Blocks.Add(para2);
        }

        return doc;
    }

    /// <summary>Erzeugt den Inline-Baum für einen Textabschnitt mit fett/kursiv/Code/Links.</summary>
    private static Span BuildInline(string text)
    {
        // Platzhalterkodierung: \uE000{b|i|c|l}text\uE001 ... Link zusätzlich \uE002url
        var encoded = Regex.Replace(text,
            @"(\*\*|__)(.+?)\1|(?<![*_])(\*|_)([^*_\s].*?)\3(?![*_])|`([^`]+)`|\[([^\]]+)\]\(([^)]+)\)",
            m =>
            {
                if (m.Groups[2].Success) return "\uE000b" + m.Groups[2].Value + "\uE001";
                if (m.Groups[4].Success) return "\uE000i" + m.Groups[4].Value + "\uE001";
                if (m.Groups[5].Success) return "\uE000c" + m.Groups[5].Value + "\uE001";
                if (m.Groups[6].Success) return "\uE000l" + m.Groups[6].Value + "\uE001\uE002" + m.Groups[7].Value;
                return m.Value;
            });

        return ApplyTokens(new Span(), encoded);
    }

    private static Span ApplyTokens(Span span, string encoded)
    {
        // Tokens in der kodierten Zeichenkette finden und sequenziell anwenden.
        var parts = new List<(string Kind, string Text, string Meta)>();
        var regex = new Regex("\uE000([bicl])(.*?)\uE001(\uE002[^ ]+)?", RegexOptions.Singleline);

        var pos = 0;
        foreach (Match m in regex.Matches(encoded))
        {
            if (m.Index > pos)
                parts.Add(("t", encoded[pos..m.Index], ""));
            parts.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success ? m.Groups[3].Value[1..] : ""));
            pos = m.Index + m.Length;
        }
        if (pos < encoded.Length)
            parts.Add(("t", encoded[pos..], ""));

        foreach (var (kind, text, meta) in parts)
        {
            switch (kind)
            {
                case "b":
                    span.Inlines.Add(new Run(text) { FontWeight = FontWeights.Bold });
                    break;
                case "i":
                    span.Inlines.Add(new Run(text) { FontStyle = FontStyles.Italic });
                    break;
                case "c":
                    span.Inlines.Add(new Run(text)
                    {
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 12.5,
                        Foreground = InlineCodeForeground
                    });
                    break;
                case "l":
                    var hyperlink = new Hyperlink { NavigateUri = new Uri(meta, UriKind.RelativeOrAbsolute) };
                    hyperlink.Inlines.Add(new Run(text) { Foreground = AccentLightBrush });
                    hyperlink.RequestNavigate += (_, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString())
                            { UseShellExecute = true });
                        }
                        catch { /* kein Browser verfügbar */ }
                    };
                    span.Inlines.Add(hyperlink);
                    break;
                default:
                    span.Inlines.Add(new Run(text));
                    break;
            }
        }

        if (span.Inlines.Count == 0) span.Inlines.Add(new Run(encoded));
        return span;
    }

    private static readonly SolidColorBrush AccentBrush = Make("#8B5CF6");
    private static readonly SolidColorBrush AccentLightBrush = Make("#A78BFA");
    private static readonly SolidColorBrush SurfaceBrush = Make("#1B1B25");
    private static readonly SolidColorBrush MutedBorder = Make("#30303D");
    private static readonly SolidColorBrush CodeForeground = Make("#C9C4E6");
    private static readonly SolidColorBrush InlineCodeForeground = Make("#F0ABFC");

    private static SolidColorBrush Make(string hex) =>
        new(ColorConverter.ConvertFromString(hex) is Color c ? c : Color.FromRgb(0xF4, 0xF3, 0xFA));
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TinyHex.Models;

namespace TinyHex.Behaviors;

/// <summary>
/// Shows a <see cref="HexRow"/> in a <see cref="TextBlock"/> with its search hits highlighted in both the hex and the
/// ASCII column: set <c>Row</c> to the row and <c>Highlights</c> to the hits. The colors are the resources
/// <c>SearchMatchBrush</c>, <c>SearchCurrentBrush</c> and <c>SearchCurrentTextBrush</c>.
/// </summary>
public static class SearchHighlight
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.RegisterAttached("Row", typeof(HexRow), typeof(SearchHighlight),
            new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty HighlightsProperty =
        DependencyProperty.RegisterAttached("Highlights", typeof(SearchHighlights), typeof(SearchHighlight),
            new PropertyMetadata(null, OnChanged));

    public static HexRow? GetRow(TextBlock element) => (HexRow?)element.GetValue(RowProperty);

    public static void SetRow(TextBlock element, HexRow? value) => element.SetValue(RowProperty, value);

    public static SearchHighlights? GetHighlights(TextBlock element) =>
        (SearchHighlights?)element.GetValue(HighlightsProperty);

    public static void SetHighlights(TextBlock element, SearchHighlights? value) =>
        element.SetValue(HighlightsProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
            return;

        var row = GetRow(textBlock);
        var text = row?.ToString() ?? "";
        var highlights = row is not null && GetHighlights(textBlock) is { } all
            ? all.For(row.Offset, row.Bytes.Length)
            : [];
        if (highlights.Count == 0)
        {
            textBlock.Text = text;
            return;
        }

        // the same byte ranges in the hex column, then in the ASCII column; both in ascending order
        var spans = new List<(int Start, int End, bool IsCurrent)>(2 * highlights.Count);
        foreach (var h in highlights)
            spans.Add((HexRowFormatter.HexColumn(row!, h.Start), HexRowFormatter.HexColumn(row!, h.Start + h.Length - 1) + 2,
                h.IsCurrent));
        foreach (var h in highlights)
            spans.Add((HexRowFormatter.AsciiColumn(row!, h.Start), HexRowFormatter.AsciiColumn(row!, h.Start + h.Length),
                h.IsCurrent));

        textBlock.Inlines.Clear();
        var position = 0;
        foreach (var (start, end, isCurrent) in spans)
        {
            if (start > position)
                textBlock.Inlines.Add(new Run(text[position..start]));

            var run = new Run(text[start..end]);
            run.SetResourceReference(TextElement.BackgroundProperty,
                isCurrent ? "SearchCurrentBrush" : "SearchMatchBrush");
            if (isCurrent)
                run.SetResourceReference(TextElement.ForegroundProperty, "SearchCurrentTextBrush");
            textBlock.Inlines.Add(run);
            position = end;
        }
        if (position < text.Length)
            textBlock.Inlines.Add(new Run(text[position..]));
    }
}

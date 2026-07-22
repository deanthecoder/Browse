// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Globalization;
using Avalonia.Data.Converters;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;

namespace Browse.Converters;

/// <summary>
/// Selects AvaloniaEdit's bundled syntax definition from a previewed file extension.
/// </summary>
public sealed class CodeHighlightingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var extension = Path.GetExtension(value as string ?? string.Empty);
        return HighlightingManager.Instance.GetDefinitionByExtension(extension);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Wraps preview text in AvaloniaEdit's bindable document model.
/// </summary>
public sealed class CodeDocumentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new TextDocument(value as string ?? string.Empty);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

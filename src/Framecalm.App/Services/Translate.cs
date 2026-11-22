using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using Framecalm.Core;

namespace Framecalm.App.Services;

/// <summary>
/// Puts the interface in the chosen language. Text written straight into the XAML is translated when each
/// element loads; text that comes from view models goes through <see cref="TrConverter"/> or L.T/L.F in code.
/// </summary>
public static class Translate
{
    /// <summary>Picks the language before any window exists: --lang, then Settings, then the Windows language.</summary>
    public static void Choose(string? fromArgs)
    {
        var choice = fromArgs;
        if (choice is null)
        {
            try
            {
                var file = Path.Combine(Paths.State, "settings.json");
                if (File.Exists(file))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Name.Equals("language", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                            choice = p.Value.GetString();
                }
            }
            catch { }
        }
        L.Use(choice);
        if (L.Lang == "en") return;

        // dates and numbers on screen follow the language; the app's own parsing stays invariant
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag)));
        CultureInfo.DefaultThreadCurrentUICulture = L.Culture;
        Thread.CurrentThread.CurrentUICulture = L.Culture;

        // WPF only raises Loaded on elements that have their own Loaded handler, so a class handler would miss most of
        // the UI. SizeChanged reaches every element the first time it is laid out, which is exactly when text matters.
        EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((s, _) => OnLoaded(s, null!)));
        EventManager.RegisterClassHandler(typeof(ContentControl), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((s, _) => OnLoaded(s, null!)));
        EventManager.RegisterClassHandler(typeof(TextBox), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((s, _) => OnLoaded(s, null!)));
    }

    static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        Literal(fe, FrameworkElement.ToolTipProperty);
        switch (fe)
        {
            case TextBlock tb:
                if (!Literal(tb, TextBlock.TextProperty)) foreach (var i in tb.Inlines) Inline(i);
                break;
            case HeaderedContentControl h:
                Literal(h, HeaderedContentControl.HeaderProperty);
                Literal(h, ContentControl.ContentProperty);
                break;
            case ContentControl cc:
                Literal(cc, ContentControl.ContentProperty);
                break;
            case TextBox t:
                Literal(t, FrameworkElement.TagProperty); // placeholder text lives in Tag
                break;
        }
    }

    static void Inline(Inline i)
    {
        switch (i)
        {
            case Run r: Literal(r, Run.TextProperty); break;
            case Span s: foreach (var c in s.Inlines) Inline(c); break;
        }
    }

    /// <summary>Translates a property set to plain text in XAML (not bound, not styled). True if it held text.</summary>
    static bool Literal(DependencyObject d, DependencyProperty p)
    {
        if (BindingOperations.IsDataBound(d, p)) return true;
        var src = DependencyPropertyHelper.GetValueSource(d, p);
        if (src.IsExpression) return true;
        // text written in the XAML itself, or set by the template the element lives in
        if (src.BaseValueSource is not (BaseValueSource.Local or BaseValueSource.ParentTemplate or BaseValueSource.ParentTemplateTrigger)) return false;
        if (d.GetValue(p) is not string s || s.Length == 0) return false;
        var t = L.T(s);
        if (t != s) d.SetCurrentValue(p, t);
        return true;
    }
}

/// <summary>Binding converter for text that comes from code or data: Text="{Binding X, Converter={StaticResource Tr}}".</summary>
public sealed class TrConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        string s => L.T(s),
        null => null,
        _ when targetType == typeof(string) => value.ToString(),
        _ => value,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}

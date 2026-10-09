using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace WGS.Views;

/// <summary>
/// Text editor for config/text files with Notepad++-style behaviour: line numbers, syntax colours, Ctrl+F / Ctrl+H / Ctrl+G,
/// F3 / Shift+F3, Ctrl+D (duplicate line), Ctrl+L (delete line), Ctrl+Shift+Up/Down (move line), Ctrl+Q (toggle comment),
/// Alt+Z (word wrap), Ctrl+wheel (zoom), Ctrl+S (save). Binds like a TextBox: <c>Text</c> two-way, <c>FileName</c> picks the colouring.
/// </summary>
public partial class CodeEditor : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CodeEditor),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

    public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(
        nameof(FileName), typeof(string), typeof(CodeEditor),
        new PropertyMetadata(string.Empty, (d, _) => ((CodeEditor)d).ApplyFileType()));

    public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(
        nameof(SaveCommand), typeof(ICommand), typeof(CodeEditor));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string FileName { get => (string)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public ICommand? SaveCommand { get => (ICommand?)GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }

    // Word wrap and zoom carry over between editors for the rest of the session.
    private static bool   _wrapDefault;
    private static double _fontSizeDefault = 13;

    private static readonly Dictionary<string, IHighlightingDefinition?> Highlightings = new();

    private bool _syncing;

    public CodeEditor()
    {
        InitializeComponent();

        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableHyperlinks     = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.ConvertTabsToSpaces  = false;
        Editor.Options.IndentationSize      = 4;
        Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(0x1C, 0x23, 0x2E));
        Editor.TextArea.TextView.CurrentLineBorder     = new Pen(Brushes.Transparent, 0);
        Editor.TextArea.SelectionBrush      = new SolidColorBrush(Color.FromRgb(0x26, 0x4F, 0x78));
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.SelectionBorder     = null;
        Editor.TextArea.Caret.CaretBrush    = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3));

        Editor.FontSize = _fontSizeDefault;
        WrapToggle.IsChecked = _wrapDefault;
        Editor.WordWrap = _wrapDefault;

        Editor.TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _syncing = true;
                SetCurrentValue(TextProperty, Editor.Text);
                _syncing = false;
            }
            UpdateEol();
            UpdateStatus();
        };
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatus();
        Editor.TextArea.SelectionChanged      += (_, _) => UpdateStatus();
        PreviewKeyDown += OnPreviewKeyDown;
        UpdateStatus();
    }

    /// <summary>Puts the caret in the editor (used when a file has just been opened).</summary>
    public void FocusEditor() => Editor.TextArea.Focus();

    // ── Text / file type ────────────────────────────────────────────────────────

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CodeEditor)d;
        if (c._syncing) return;
        var s = e.NewValue as string ?? string.Empty;
        if (c.Editor.Text == s) return;
        c._syncing = true;
        c.Editor.Text = s;
        // A different file / reloaded content: undo must not step back into the previous text.
        c.Editor.Document.UndoStack.ClearAll();
        c._syncing = false;
        c.Editor.CaretOffset = 0;
        c.Editor.ScrollToHome();
        c.UpdateEol();
        c.UpdateStatus();
        if (c.FindBar.Visibility == Visibility.Visible) c.UpdateFindInfo();
    }

    private string Extension => Path.GetExtension(FileName ?? string.Empty).ToLowerInvariant();

    private void ApplyFileType()
    {
        var kind = Extension switch
        {
            ".json" => "Json",
            ".xml" or ".xaml" or ".config" or ".csproj" or ".props" or ".targets" or ".html" or ".htm" or ".svg" or ".manifest" or ".resx" => "Xml",
            _ => "Config",
        };
        if (!Highlightings.TryGetValue(kind, out var def))
        {
            def = LoadHighlighting(kind);
            Highlightings[kind] = def;
        }
        Editor.SyntaxHighlighting = def;
    }

    private static IHighlightingDefinition? LoadHighlighting(string name)
    {
        try
        {
            var asm = typeof(CodeEditor).Assembly;
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + name + ".xshd", StringComparison.OrdinalIgnoreCase));
            if (res == null) return null;
            using var stream = asm.GetManifestResourceStream(res)!;
            using var reader = new XmlTextReader(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch { return null; }   // no colouring is better than no editor
    }

    // ── Status bar ──────────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        var caret = Editor.TextArea.Caret;
        var sel   = Editor.SelectionLength;
        PosText.Text = $"Ln {caret.Line:N0}, Col {caret.Column:N0}" + (sel > 0 ? $"    Sel {sel:N0}" : "");
        LenText.Text = $"{Editor.Document.TextLength:N0} chars · {Editor.Document.LineCount:N0} lines";
    }

    private void UpdateEol()
    {
        var text = Editor.Document.Text;
        var i = text.IndexOf('\n');
        EolText.Text = i < 0 ? "" : (i > 0 && text[i - 1] == '\r' ? "CRLF" : "LF");
    }

    // ── Keyboard ────────────────────────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandleKey(key, Keyboard.Modifiers, Editor.TextArea.IsKeyboardFocusWithin)) e.Handled = true;
    }

    /// <summary>The editor's shortcut table. Returns true when the key was a shortcut (and so must not reach the text area).</summary>
    internal bool HandleKey(Key key, ModifierKeys mods, bool inEditor)
    {
        var ctrl  = (mods & ModifierKeys.Control) != 0;
        var shift = (mods & ModifierKeys.Shift) != 0;
        var alt   = (mods & ModifierKeys.Alt) != 0;

        if (ctrl && !alt && key == Key.F)   { ShowBar(replace: false, goTo: false); return true; }
        if (ctrl && !alt && key == Key.H)   { ShowBar(replace: true,  goTo: false); return true; }
        if (ctrl && !alt && key == Key.G)   { ShowBar(replace: false, goTo: true);  return true; }
        if (key == Key.F3 && !ctrl && !alt) { FindNext(forward: !shift, fromSelectionStart: false); return true; }
        if (alt && !ctrl && key == Key.Z)   { WrapToggle.IsChecked = WrapToggle.IsChecked != true; return true; }
        if (key == Key.Escape && FindBar.Visibility == Visibility.Visible) { CloseBar(); return true; }

        if (!inEditor) return false;

        if (ctrl && !alt && !shift && key == Key.S)
        {
            if (SaveCommand?.CanExecute(null) != true) return false;
            SaveCommand.Execute(null);
            return true;
        }
        if (ctrl && !alt && !shift && key == Key.D) { DuplicateLine(); return true; }
        if (ctrl && !alt && !shift && key == Key.L) { DeleteLines();   return true; }
        if (ctrl && !alt && !shift && key == Key.Q) { ToggleComment(); return true; }
        if (ctrl && !alt && shift && key == Key.Up)   { MoveLines(up: true);  return true; }
        if (ctrl && !alt && shift && key == Key.Down) { MoveLines(up: false); return true; }
        return false;
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        _fontSizeDefault = Math.Clamp(Editor.FontSize + (e.Delta > 0 ? 1 : -1), 8, 40);
        Editor.FontSize = _fontSizeDefault;
        e.Handled = true;
    }

    private void WrapToggle_Changed(object sender, RoutedEventArgs e)
    {
        _wrapDefault = WrapToggle.IsChecked == true;
        Editor.WordWrap = _wrapDefault;
    }

    // ── Line operations ─────────────────────────────────────────────────────────

    /// <summary>The lines touched by the selection. A selection that ends at the very start of a line doesn't include that line.</summary>
    private (DocumentLine First, DocumentLine Last) SelectedLines()
    {
        var doc   = Editor.Document;
        var start = Editor.SelectionStart;
        var end   = start + Editor.SelectionLength;
        var first = doc.GetLineByOffset(start);
        var last  = doc.GetLineByOffset(end);
        if (end > start && last.Offset == end && last.LineNumber > first.LineNumber)
            last = last.PreviousLine;
        return (first, last);
    }

    private void DuplicateLine()
    {
        var doc = Editor.Document;
        doc.UndoStack.StartUndoGroup();
        try
        {
            if (Editor.SelectionLength > 0)
            {
                var text = Editor.SelectedText;
                var end  = Editor.SelectionStart + Editor.SelectionLength;
                doc.Insert(end, text);
                Editor.Select(end, text.Length);
            }
            else
            {
                var line  = doc.GetLineByOffset(Editor.CaretOffset);
                var delim = line.DelimiterLength > 0 ? doc.GetText(line.EndOffset, line.DelimiterLength) : Environment.NewLine;
                var col   = Editor.CaretOffset - line.Offset;
                var text  = doc.GetText(line.Offset, line.Length);
                var end   = line.EndOffset;
                doc.Insert(end, delim + text);
                Editor.CaretOffset = end + delim.Length + col;
            }
        }
        finally { doc.UndoStack.EndUndoGroup(); }
    }

    private void DeleteLines()
    {
        var doc = Editor.Document;
        var (first, last) = SelectedLines();
        var start = first.Offset;
        var end   = last.EndOffset + last.DelimiterLength;
        // Deleting the final line: also take the line break before it so no empty line is left behind.
        if (last.DelimiterLength == 0 && first.PreviousLine is { } prev) start = prev.EndOffset;
        doc.Remove(start, end - start);
    }

    private void MoveLines(bool up)
    {
        var doc = Editor.Document;
        var (first, last) = SelectedLines();
        var selStart = Editor.SelectionStart;
        var selLen   = Editor.SelectionLength;
        var caret    = Editor.CaretOffset;
        var block    = doc.GetText(first.Offset, last.EndOffset - first.Offset);

        doc.UndoStack.StartUndoGroup();
        try
        {
            int shift;
            if (up)
            {
                var prev = first.PreviousLine;
                if (prev == null) return;
                var prevText = doc.GetText(prev.Offset, prev.Length);
                var delim    = doc.GetText(prev.EndOffset, prev.DelimiterLength);
                shift = -(prev.Length + prev.DelimiterLength);   // before the replace: the line objects are gone afterwards
                doc.Replace(prev.Offset, last.EndOffset - prev.Offset, block + delim + prevText);
            }
            else
            {
                var next = last.NextLine;
                if (next == null) return;
                var nextText = doc.GetText(next.Offset, next.Length);
                var delim    = doc.GetText(last.EndOffset, last.DelimiterLength);
                shift = next.Length + delim.Length;
                doc.Replace(first.Offset, next.EndOffset - first.Offset, nextText + delim + block);
            }
            if (selLen > 0) Editor.Select(selStart + shift, selLen);
            else            Editor.CaretOffset = caret + shift;
        }
        finally { doc.UndoStack.EndUndoGroup(); }
        Editor.TextArea.Caret.BringCaretToView();
    }

    /// <summary>The comment marker for the file type (null = the format has no comments, e.g. JSON).</summary>
    private string? LineCommentPrefix() => Extension switch
    {
        ".json" => null,
        ".ini" => ";",
        ".yml" or ".yaml" or ".properties" or ".conf" or ".toml" or ".env" or ".sh" or ".txt" or ".log" => "#",
        _ => "//",
    };

    private static bool IsXmlLike(string ext) =>
        ext is ".xml" or ".xaml" or ".config" or ".csproj" or ".props" or ".targets" or ".html" or ".htm" or ".svg" or ".manifest" or ".resx";

    private void ToggleComment()
    {
        var doc = Editor.Document;
        var (first, last) = SelectedLines();

        if (IsXmlLike(Extension))
        {
            var start = first.Offset;
            var text  = doc.GetText(start, last.EndOffset - start);
            var t     = text.Trim();
            doc.UndoStack.StartUndoGroup();
            try
            {
                if (t.StartsWith("<!--") && t.EndsWith("-->"))
                {
                    var s = text.IndexOf("<!--", StringComparison.Ordinal);
                    var e = text.LastIndexOf("-->", StringComparison.Ordinal);
                    var inner = text[(s + 4)..e];
                    if (inner.StartsWith(' ')) inner = inner[1..];
                    if (inner.EndsWith(' ')) inner = inner[..^1];
                    doc.Replace(start + s, e + 3 - s, inner);
                }
                else
                {
                    var indent = text.Length - text.TrimStart().Length;
                    doc.Insert(start + indent + text.TrimStart().TrimEnd().Length, " -->");
                    doc.Insert(start + indent, "<!-- ");
                }
            }
            finally { doc.UndoStack.EndUndoGroup(); }
            return;
        }

        var prefix = LineCommentPrefix();
        if (prefix == null) return;

        var lines = new List<DocumentLine>();
        for (var l = first; l != null && l.LineNumber <= last.LineNumber; l = l.NextLine) lines.Add(l);
        var content = lines.Where(l => doc.GetText(l.Offset, l.Length).Trim().Length > 0).ToList();
        if (content.Count == 0) return;

        var allCommented = content.All(l => doc.GetText(l.Offset, l.Length).TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        doc.UndoStack.StartUndoGroup();
        try
        {
            if (allCommented)
            {
                foreach (var l in Enumerable.Reverse(content))
                {
                    var text   = doc.GetText(l.Offset, l.Length);
                    var indent = text.Length - text.TrimStart().Length;
                    var remove = prefix.Length + (text.Length > indent + prefix.Length && text[indent + prefix.Length] == ' ' ? 1 : 0);
                    doc.Remove(l.Offset + indent, remove);
                }
            }
            else
            {
                var indent = content.Min(l =>
                {
                    var text = doc.GetText(l.Offset, l.Length);
                    return text.Length - text.TrimStart().Length;
                });
                foreach (var l in Enumerable.Reverse(content))
                    doc.Insert(l.Offset + indent, prefix + " ");
            }
        }
        finally { doc.UndoStack.EndUndoGroup(); }
    }

    // ── Find / replace / go to line ─────────────────────────────────────────────

    private void ShowBar(bool replace, bool goTo)
    {
        FindBar.Visibility    = Visibility.Visible;
        FindRow.Visibility    = goTo ? Visibility.Collapsed : Visibility.Visible;
        ReplaceRow.Visibility = replace && !goTo ? Visibility.Visible : Visibility.Collapsed;
        GotoRow.Visibility    = goTo ? Visibility.Visible : Visibility.Collapsed;

        if (goTo)
        {
            GotoInfo.Text = $"line 1 – {Editor.Document.LineCount:N0}   (now {Editor.TextArea.Caret.Line:N0})";
            GotoBox.Text = "";
            GotoBox.Focus();
            return;
        }

        // Start from what's selected, like Notepad++ (single-line selections only).
        var sel = Editor.SelectedText;
        if (sel.Length > 0 && sel.Length < 200 && !sel.Contains('\n') && !sel.Contains('\r'))
            FindBox.Text = sel;
        FindBox.Focus();
        FindBox.SelectAll();
        UpdateFindInfo();
    }

    private void CloseBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        Editor.TextArea.Focus();
    }

    private void CloseBar_Click(object sender, RoutedEventArgs e) => CloseBar();
    private void MenuFind_Click(object sender, RoutedEventArgs e)    => ShowBar(replace: false, goTo: false);
    private void MenuReplace_Click(object sender, RoutedEventArgs e) => ShowBar(replace: true,  goTo: false);
    private void MenuGoto_Click(object sender, RoutedEventArgs e)    => ShowBar(replace: false, goTo: true);

    private Regex? BuildRegex(bool rightToLeft = false)
    {
        var pattern = FindBox.Text;
        if (pattern.Length == 0) return null;
        try
        {
            var options = RegexOptions.CultureInvariant;
            if (MatchCaseBox.IsChecked != true) options |= RegexOptions.IgnoreCase;
            if (rightToLeft) options |= RegexOptions.RightToLeft;
            return new Regex(RegexBox.IsChecked == true ? pattern : Regex.Escape(pattern), options);
        }
        catch (ArgumentException)
        {
            FindInfo.Text = "Invalid regex";
            return null;
        }
    }

    private void UpdateFindInfo()
    {
        if (FindBox.Text.Length == 0) { FindInfo.Text = ""; return; }
        var regex = BuildRegex();
        if (regex == null) return;
        var count = 0;
        try
        {
            foreach (Match m in regex.Matches(Editor.Document.Text))
                if (m.Length > 0 && ++count >= 10000) break;
        }
        catch (RegexMatchTimeoutException) { }
        FindInfo.Text = count == 0 ? "No matches" : count >= 10000 ? "10000+ matches" : count == 1 ? "1 match" : $"{count:N0} matches";
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateFindInfo();
        FindNext(forward: true, fromSelectionStart: true);   // incremental: extend the current match while typing
    }

    private void Option_Click(object sender, RoutedEventArgs e)
    {
        UpdateFindInfo();
        FindNext(forward: true, fromSelectionStart: true);
    }

    private void FindBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext(forward: (Keyboard.Modifiers & ModifierKeys.Shift) == 0, fromSelectionStart: false);
            e.Handled = true;
        }
    }

    private void ReplaceBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Replace_Click(sender, e); e.Handled = true; }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext(forward: true,  fromSelectionStart: false);
    private void FindPrev_Click(object sender, RoutedEventArgs e) => FindNext(forward: false, fromSelectionStart: false);

    /// <summary>Selects the next/previous match, wrapping around at the ends. Returns false when there is none.</summary>
    private bool FindNext(bool forward, bool fromSelectionStart)
    {
        if (FindBox.Text.Length == 0) return false;
        var regex = BuildRegex(rightToLeft: !forward);
        if (regex == null) return false;

        var text = Editor.Document.Text;
        var selStart = Editor.SelectionStart;
        var selEnd   = selStart + Editor.SelectionLength;
        Match m;
        try
        {
            if (forward)
            {
                var from = fromSelectionStart ? selStart : selEnd;
                m = NextNonEmpty(regex, text, from);
                if (!m.Success && from > 0) m = NextNonEmpty(regex, text, 0);
            }
            else
            {
                m = regex.Match(text, selStart);
                if (!m.Success && selStart < text.Length) m = regex.Match(text, text.Length);
            }
        }
        catch (RegexMatchTimeoutException) { return false; }

        if (!m.Success || m.Length == 0)
        {
            FindInfo.Text = "No matches";
            return false;
        }
        Editor.Select(m.Index, m.Length);
        Editor.TextArea.Caret.BringCaretToView();
        Editor.ScrollTo(Editor.Document.GetLineByOffset(m.Index).LineNumber, 0);
        return true;
    }

    private static Match NextNonEmpty(Regex regex, string text, int from)
    {
        var m = regex.Match(text, from);
        while (m.Success && m.Length == 0) m = m.NextMatch();
        return m;
    }

    private string ReplacementFor(Match m)
    {
        var replacement = ReplaceBox.Text;
        return RegexBox.IsChecked == true ? m.Result(replacement) : replacement;
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;
        // Replace the current selection only if it is itself a match; otherwise just move to the next one.
        if (Editor.SelectionLength > 0)
        {
            var start = Editor.SelectionStart;
            var m = regex.Match(Editor.Document.Text, start);
            if (m.Success && m.Index == start && m.Length == Editor.SelectionLength)
            {
                Editor.Document.Replace(start, m.Length, ReplacementFor(m));
                Editor.Select(start + ReplacementFor(m).Length, 0);
            }
        }
        FindNext(forward: true, fromSelectionStart: false);
        UpdateFindInfo();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;
        var doc = Editor.Document;
        var matches = regex.Matches(doc.Text).Cast<Match>().Where(m => m.Length > 0).ToList();
        if (matches.Count == 0) { FindInfo.Text = "No matches"; return; }

        doc.UndoStack.StartUndoGroup();
        try
        {
            // Back to front so the offsets of the remaining matches stay valid.
            foreach (var m in Enumerable.Reverse(matches))
                doc.Replace(m.Index, m.Length, ReplacementFor(m));
        }
        finally { doc.UndoStack.EndUndoGroup(); }
        FindInfo.Text = $"{matches.Count:N0} replaced";
    }

    private void GotoBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private void GotoBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        GoToLine(GotoBox.Text);
    }

    internal void GoToLine(string text)
    {
        if (!int.TryParse(text, out var n)) return;
        n = Math.Clamp(n, 1, Editor.Document.LineCount);
        var line = Editor.Document.GetLineByNumber(n);
        Editor.CaretOffset = line.Offset;
        Editor.Select(line.Offset, 0);
        Editor.ScrollTo(n, 0);
        CloseBar();
    }
}

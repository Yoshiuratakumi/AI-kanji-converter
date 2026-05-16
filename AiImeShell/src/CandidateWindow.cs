using System.Drawing;
using System.Windows.Forms;

namespace AiImeShell;

/// <summary>
/// Floating candidate list window. Call Show(candidates) to display.
/// The SelectedIndex property tracks the current selection.
/// Subscribe to Committed(string) to receive the confirmed candidate.
/// </summary>
internal sealed class CandidateWindow : Form
{
    public event Action<string>? Committed;

    private readonly ListBox _list;
    private string[] _candidates = [];

    public int SelectedIndex
    {
        get => _list.SelectedIndex;
        set
        {
            if (value >= 0 && value < _list.Items.Count)
                _list.SelectedIndex = value;
        }
    }

    public string? SelectedCandidate =>
        _list.SelectedIndex >= 0 ? _candidates[_list.SelectedIndex] : null;

    public CandidateWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar    = false;
        TopMost          = true;
        BackColor        = Color.FromArgb(0x2B, 0x2B, 0x2B);
        AutoSize         = false;

        _list = new ListBox
        {
            Dock            = DockStyle.Fill,
            BackColor       = Color.FromArgb(0x2B, 0x2B, 0x2B),
            ForeColor       = Color.White,
            Font            = new Font("Meiryo UI", 13f, FontStyle.Regular),
            BorderStyle     = BorderStyle.None,
            SelectionMode   = SelectionMode.One,
            ItemHeight      = 28,
        };
        _list.DoubleClick += (_, _) => CommitSelected();
        Controls.Add(_list);
    }

    public void ShowAt(Point screenPt, string[] candidates)
    {
        _candidates = candidates;
        _list.Items.Clear();
        for (int i = 0; i < candidates.Length; i++)
            _list.Items.Add($"{i + 1}. {candidates[i]}");

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;

        int itemH  = _list.ItemHeight;
        int rows   = Math.Min(candidates.Length, 9);
        int width  = 200;
        // Measure widest item
        using var g = _list.CreateGraphics();
        foreach (var item in _list.Items)
        {
            int w = (int)g.MeasureString(item.ToString(), _list.Font).Width + 24;
            if (w > width) width = w;
        }

        Size = new Size(width, rows * itemH + 4);
        Location = AdjustToScreen(screenPt, Size);
        if (!Visible) Show();
    }

    public void MoveDown()
    {
        if (_list.Items.Count == 0) return;
        int next = (_list.SelectedIndex + 1) % _list.Items.Count;
        _list.SelectedIndex = next;
    }

    public void MoveUp()
    {
        if (_list.Items.Count == 0) return;
        int prev = (_list.SelectedIndex - 1 + _list.Items.Count) % _list.Items.Count;
        _list.SelectedIndex = prev;
    }

    public void CommitSelected()
    {
        string? c = SelectedCandidate;
        Hide();
        if (c != null) Committed?.Invoke(c);
    }

    public new void Hide()
    {
        if (Visible) base.Hide();
    }

    private static Point AdjustToScreen(Point pt, Size size)
    {
        var screen = Screen.FromPoint(pt).WorkingArea;
        int x = pt.X;
        int y = pt.Y;
        if (x + size.Width  > screen.Right)  x = screen.Right  - size.Width;
        if (y + size.Height > screen.Bottom) y = pt.Y - size.Height - 4;
        if (x < screen.Left)   x = screen.Left;
        if (y < screen.Top)    y = screen.Top;
        return new Point(x, y);
    }

    // Prevent the window from stealing focus
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
}

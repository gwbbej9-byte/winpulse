using System.Collections;
using System.Reflection;

namespace WinPulse;

public sealed class MainForm : Form
{
    private readonly ProcessSampler _sampler = new();
    private readonly ListView _list;
    private readonly TextBox _filter;
    private readonly Label _host;
    private readonly Label _cpu;
    private readonly Label _ram;
    private readonly ToolStripStatusLabel _statusCount;
    private readonly ToolStripStatusLabel _statusAge;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly RowComparer _comparer = new();

    private IReadOnlyList<ProcessSnapshot> _latest = Array.Empty<ProcessSnapshot>();
    private DateTime _lastSampleUtc = DateTime.MinValue;
    private int _selectedPid = -1;
    private int _sortCol = 2;
    private SortOrder _sortOrder = SortOrder.Descending;
    private int _sampleGate;

    public MainForm()
    {
        Text = "winpulse";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Fg;
        ClientSize = new Size(900, 560);
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Theme.Bg,
            Padding = new Padding(12, 8, 12, 0)
        };

        _host = MakeLabel(Theme.Muted, ContentAlignment.MiddleLeft);
        _host.Text = Environment.MachineName;
        _cpu = MakeLabel(Theme.Accent, ContentAlignment.MiddleCenter);
        _cpu.Text = "CPU  —";
        _ram = MakeLabel(Theme.Fg, ContentAlignment.MiddleRight);
        _ram.Text = "RAM  —";

        var topGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Bg
        };
        topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
        topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        topGrid.Controls.Add(_host, 0, 0);
        topGrid.Controls.Add(_cpu, 1, 0);
        topGrid.Controls.Add(_ram, 2, 0);
        top.Controls.Add(topGrid);

        var filterBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Theme.Bg,
            Padding = new Padding(12, 4, 12, 4)
        };
        var filterLabel = MakeLabel(Theme.Muted, ContentAlignment.MiddleLeft);
        filterLabel.Text = "filter";
        filterLabel.AutoSize = true;
        filterLabel.Dock = DockStyle.Left;
        filterLabel.Padding = new Padding(0, 0, 8, 0);

        _filter = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.Fg,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };
        _filter.TextChanged += (_, _) => RebuildList();
        _filter.HandleCreated += (_, _) =>
            Native.SendMessage(_filter.Handle, Native.EmSetCueBanner, (IntPtr)1, "name contains…");

        filterBar.Controls.Add(_filter);
        filterBar.Controls.Add(filterLabel);

        var status = new StatusStrip
        {
            BackColor = Theme.Status,
            ForeColor = Theme.Muted,
            SizingGrip = false,
            Font = new Font("Segoe UI", 8.5f),
            Renderer = new DarkStripRenderer()
        };
        _statusCount = new ToolStripStatusLabel("0 processes")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted
        };
        _statusAge = new ToolStripStatusLabel("refreshed 0s ago")
        {
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.Muted
        };
        status.Items.AddRange(new ToolStripItem[] { _statusCount, _statusAge });

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Clickable,
            BorderStyle = BorderStyle.None,
            OwnerDraw = true,
            BackColor = Theme.Bg,
            ForeColor = Theme.Fg,
            Font = new Font("Segoe UI", 9f),
            HideSelection = false
        };
        EnableDoubleBuffer(_list);
        _list.Columns.Add("Name", 220);
        _list.Columns.Add("PID", 70);
        _list.Columns.Add("WS (MB)", 90);
        _list.Columns.Add("Peak (MB)", 90);
        _list.Columns.Add("CPU %", 80);
        _list.Columns.Add("Threads", 80);
        _list.ColumnClick += OnColumnClick;
        _list.DrawColumnHeader += OnDrawHeader;
        _list.DrawItem += (_, e) => { e.DrawDefault = false; };
        _list.DrawSubItem += OnDrawSubItem;
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is ProcessSnapshot row)
                _selectedPid = row.Pid;
        };
        _list.Resize += (_, _) => StretchNameColumn();

        Controls.Add(_list);
        Controls.Add(status);
        Controls.Add(filterBar);
        Controls.Add(top);

        _comparer.Column = _sortCol;
        _comparer.Order = _sortOrder;
        _list.ListViewItemSorter = _comparer;

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => _ = SampleAndPaintAsync();

        Load += (_, _) =>
        {
            StretchNameColumn();
            _ = SampleAndPaintAsync();
            _timer.Start();
        };
        FormClosed += (_, _) => _timer.Stop();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _filter.Clear();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private async Task SampleAndPaintAsync()
    {
        if (Interlocked.Exchange(ref _sampleGate, 1) != 0)
            return;
        try
        {
            await Task.Run(() => _sampler.Tick()).ConfigureAwait(true);
            if (IsDisposed)
                return;

            _latest = _sampler.Processes;
            _lastSampleUtc = DateTime.UtcNow;

            var host = _sampler.Host;
            _host.Text = host.MachineName;
            _cpu.Text = "CPU  " + (double.IsNaN(host.CpuPercent)
                ? "—"
                : FormatUtil.CpuPercent(host.CpuPercent) + "%");
            _ram.Text = host.RamTotalBytes == 0 ? "RAM  —" : "RAM  " + FormatUtil.RamGb(host.RamUsedBytes, host.RamTotalBytes);

            RebuildList();
        }
        catch (Exception)
        {
            // keep the 1s timer alive if a tick faults
        }
        finally
        {
            Interlocked.Exchange(ref _sampleGate, 0);
        }
    }

    private void RebuildList()
    {
        string filter = _filter.Text;
        var rows = _latest.Where(p => FormatUtil.NameContains(p.Name, filter)).ToList();
        SortRows(rows);

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            ListViewItem? select = null;
            foreach (var p in rows)
            {
                var item = new ListViewItem(p.Name) { Tag = p };
                item.SubItems.Add(p.Pid.ToString());
                item.SubItems.Add(FormatUtil.WorkingSetMb(p.WorkingSetBytes));
                item.SubItems.Add(FormatUtil.WorkingSetMb(p.PeakWorkingSetBytes));
                item.SubItems.Add(FormatUtil.CpuPercent(p.CpuPercent));
                item.SubItems.Add(p.ThreadCount.ToString());
                _list.Items.Add(item);
                if (p.Pid == _selectedPid)
                    select = item;
            }
            if (select != null)
                select.Selected = true;
        }
        finally
        {
            _list.EndUpdate();
        }

        _statusCount.Text = FormatUtil.ProcessCount(rows.Count);
        var age = _lastSampleUtc == DateTime.MinValue ? TimeSpan.Zero : DateTime.UtcNow - _lastSampleUtc;
        _statusAge.Text = FormatUtil.RefreshAge(age);
    }

    private void SortRows(List<ProcessSnapshot> rows)
    {
        int dir = _sortOrder == SortOrder.Ascending ? 1 : -1;
        rows.Sort((a, b) =>
        {
            int c = _sortCol switch
            {
                1 => a.Pid.CompareTo(b.Pid),
                2 => a.WorkingSetBytes.CompareTo(b.WorkingSetBytes),
                3 => a.PeakWorkingSetBytes.CompareTo(b.PeakWorkingSetBytes),
                4 => NanLast(a.CpuPercent).CompareTo(NanLast(b.CpuPercent)),
                5 => a.ThreadCount.CompareTo(b.ThreadCount),
                _ => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
            };
            if (c == 0)
                c = a.Pid.CompareTo(b.Pid);
            return c * dir;
        });
    }

    private static double NanLast(double v) => double.IsNaN(v) ? -1 : v;

    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column == _sortCol)
            _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        else
        {
            _sortCol = e.Column;
            _sortOrder = e.Column == 0 ? SortOrder.Ascending : SortOrder.Descending;
        }
        _comparer.Column = _sortCol;
        _comparer.Order = _sortOrder;
        RebuildList();
        _list.Invalidate();
    }

    private void OnDrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var bg = new SolidBrush(Theme.Header);
        e.Graphics.FillRectangle(bg, e.Bounds);
        using var accent = new Pen(Theme.Accent);
        e.Graphics.DrawLine(accent, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        string title = e.Header?.Text ?? "";
        if (e.ColumnIndex == _sortCol)
            title += _sortOrder == SortOrder.Ascending ? "  ▲" : "  ▼";

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (e.ColumnIndex >= 1)
            flags |= TextFormatFlags.Right;
        var rect = Rectangle.Inflate(e.Bounds, -10, 0);
        TextRenderer.DrawText(e.Graphics, title, e.Font ?? Font, rect, Theme.Muted, flags);
    }

    private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        bool selected = e.Item?.Selected == true;
        var bg = selected ? Theme.Select : (e.ItemIndex % 2 == 0 ? Theme.Bg : Theme.AltRow);
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Bounds);

        if (selected && e.ColumnIndex == 0)
        {
            using var bar = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 2, e.Bounds.Height);
        }

        using (var grid = new Pen(Theme.Grid))
            e.Graphics.DrawLine(grid, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var fg = (!selected && e.ColumnIndex == 4) ? Theme.Accent : Theme.Fg;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (e.ColumnIndex >= 1)
            flags |= TextFormatFlags.Right;
        var rect = Rectangle.Inflate(e.Bounds, -10, 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", _list.Font, rect, fg, flags);
    }

    private void StretchNameColumn()
    {
        if (_list.Columns.Count < 6)
            return;
        int used = 0;
        for (int i = 1; i < _list.Columns.Count; i++)
            used += _list.Columns[i].Width;
        int name = _list.ClientSize.Width - used - 4;
        if (name > 120)
            _list.Columns[0].Width = name;
    }

    private static Label MakeLabel(Color fg, ContentAlignment align)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = fg,
            BackColor = Theme.Bg,
            TextAlign = align,
            AutoEllipsis = true
        };
    }

    private static void EnableDoubleBuffer(Control c)
    {
        typeof(Control)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(c, true);
    }

    private sealed class DarkStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkStripRenderer() : base(new DarkStripColors()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }

    private sealed class DarkStripColors : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => Theme.Status;
        public override Color ToolStripGradientMiddle => Theme.Status;
        public override Color ToolStripGradientEnd => Theme.Status;
        public override Color StatusStripGradientBegin => Theme.Status;
        public override Color StatusStripGradientEnd => Theme.Status;
    }

    private sealed class RowComparer : IComparer
    {
        public int Column;
        public SortOrder Order = SortOrder.Descending;

        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem a || y is not ListViewItem b)
                return 0;
            if (a.Tag is not ProcessSnapshot pa || b.Tag is not ProcessSnapshot pb)
                return 0;
            int c = Column switch
            {
                1 => pa.Pid.CompareTo(pb.Pid),
                2 => pa.WorkingSetBytes.CompareTo(pb.WorkingSetBytes),
                3 => pa.PeakWorkingSetBytes.CompareTo(pb.PeakWorkingSetBytes),
                4 => (double.IsNaN(pa.CpuPercent) ? -1 : pa.CpuPercent)
                    .CompareTo(double.IsNaN(pb.CpuPercent) ? -1 : pb.CpuPercent),
                5 => pa.ThreadCount.CompareTo(pb.ThreadCount),
                _ => string.Compare(pa.Name, pb.Name, StringComparison.OrdinalIgnoreCase)
            };
            return Order == SortOrder.Ascending ? c : -c;
        }
    }
}

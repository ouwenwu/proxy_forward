using System.ComponentModel;
using System.Diagnostics;

namespace ProxyForward;

public sealed class MainForm : Form
{
    private readonly ConfigRepository _repository = new();
    private readonly List<ForwardConfig> _configs;
    private readonly Dictionary<Guid, ForwardSession> _sessions = new();
    private readonly BindingList<GridRow> _rows = [];

    private readonly DataGridView _grid = new();
    private readonly Button _addButton = new();
    private readonly Button _editButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _testButton = new();
    private readonly Button _logButton = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    private bool _allowClose;

    public MainForm()
    {
        AppPaths.Ensure();
        _configs = _repository.Load();

        Text = "Proxy Forward";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 620);
        Size = new Size(1120, 680);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildUi();
        BuildTray();
        ReloadRows();

        _refreshTimer.Interval = 1200;
        _refreshTimer.Tick += (_, _) => RefreshStates();
        _refreshTimer.Start();

        AppLogger.Info("软件启动。");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        StopAll();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.DataSource = _rows;
        _grid.CellDoubleClick += (_, _) => EditSelected();

        AddColumn("Name", "名称", 150);
        AddColumn("Server", "服务器", 160);
        AddColumn("Username", "用户", 95);
        AddColumn("Auth", "认证", 70);
        AddColumn("Local", "本机", 140);
        AddColumn("Remote", "服务器监听", 150);
        AddColumn("Status", "状态", 120);
        AddColumn("Message", "信息", 260);

        root.Controls.Add(_grid, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };
        root.Controls.Add(buttons, 0, 1);

        ConfigureButton(_addButton, "添加", (_, _) => AddConfig());
        ConfigureButton(_editButton, "编辑", (_, _) => EditSelected());
        ConfigureButton(_deleteButton, "删除", (_, _) => DeleteSelected());
        ConfigureButton(_startButton, "启动", async (_, _) => await StartSelectedAsync());
        ConfigureButton(_stopButton, "停止", (_, _) => StopSelected());
        ConfigureButton(_testButton, "测试连接", async (_, _) => await TestSelectedAsync());
        ConfigureButton(_logButton, "打开日志", (_, _) => OpenLog());

        buttons.Controls.AddRange([
            _addButton,
            _editButton,
            _deleteButton,
            Spacer(),
            _startButton,
            _stopButton,
            _testButton,
            Spacer(),
            _logButton
        ]);
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
        menu.Items.Add("启动全部", null, async (_, _) => await StartAllAsync());
        menu.Items.Add("停止全部", null, (_, _) => StopAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });

        _notifyIcon.Text = "Proxy Forward";
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Visible = true;
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void AddColumn(string property, string title, int width)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = title,
            Width = width,
            AutoSizeMode = property == "Message" ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        });
    }

    private static void ConfigureButton(Button button, string text, EventHandler click)
    {
        button.Text = text;
        button.Width = 92;
        button.Height = 32;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Click += click;
    }

    private static Control Spacer() => new Label { Width = 16 };

    private void AddConfig()
    {
        using var dialog = new ConfigDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _configs.Add(dialog.Config);
        SaveAndReload();
    }

    private void EditSelected()
    {
        var config = SelectedConfig();
        if (config is null)
        {
            return;
        }

        if (IsRunning(config.Id))
        {
            MessageBox.Show("请先停止该配置再编辑。", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ConfigDialog(config);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var index = _configs.FindIndex(item => item.Id == config.Id);
        _configs[index] = dialog.Config;
        SaveAndReload();
    }

    private void DeleteSelected()
    {
        var config = SelectedConfig();
        if (config is null)
        {
            return;
        }

        if (MessageBox.Show($"删除配置“{config.Name}”？", "Proxy Forward", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        StopConfig(config.Id);
        _configs.Remove(config);
        SaveAndReload();
    }

    private async Task StartSelectedAsync()
    {
        var config = SelectedConfig();
        if (config is not null)
        {
            await StartConfigAsync(config);
        }
    }

    private async Task StartAllAsync()
    {
        foreach (var config in _configs)
        {
            await StartConfigAsync(config);
        }
    }

    private async Task StartConfigAsync(ForwardConfig config)
    {
        if (IsRunning(config.Id))
        {
            return;
        }

        StopConfig(config.Id);

        var session = new ForwardSession(config);
        _sessions[config.Id] = session;
        ReloadRows();

        try
        {
            await session.StartAsync();
            _notifyIcon.ShowBalloonTip(1800, "Proxy Forward", $"已启动：{config.Name}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            session.Status.State = ForwardState.Failed;
            session.Status.Message = ForwardSession.TranslateException(ex);
            AppLogger.Error($"启动失败：{config.Name}", ex);
            MessageBox.Show(session.Status.Message, $"启动失败：{config.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        ReloadRows();
    }

    private void StopSelected()
    {
        var config = SelectedConfig();
        if (config is not null)
        {
            StopConfig(config.Id);
            ReloadRows();
        }
    }

    private void StopAll()
    {
        foreach (var id in _sessions.Keys.ToList())
        {
            StopConfig(id);
        }
        ReloadRows();
    }

    private void StopConfig(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session))
        {
            return;
        }

        session.Dispose();
        _sessions.Remove(id);
    }

    private async Task TestSelectedAsync()
    {
        var config = SelectedConfig();
        if (config is null)
        {
            return;
        }

        using var session = new ForwardSession(config);
        try
        {
            await session.TestAsync();
            MessageBox.Show("测试通过：本机端口可连接，SSH 认证成功。", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"测试失败：{config.Name}", ex);
            MessageBox.Show(ForwardSession.TranslateException(ex), "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenLog()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.LogPath))
        {
            File.WriteAllText(AppPaths.LogPath, "");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.LogPath,
            UseShellExecute = true
        });
    }

    private void RefreshStates()
    {
        foreach (var session in _sessions.Values)
        {
            session.RefreshState();
        }
        ReloadRows(keepSelection: true);
    }

    private void SaveAndReload()
    {
        _repository.Save(_configs);
        ReloadRows();
    }

    private void ReloadRows(bool keepSelection = false)
    {
        var selectedId = keepSelection ? SelectedConfig()?.Id : null;
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();

        foreach (var config in _configs)
        {
            var status = _sessions.TryGetValue(config.Id, out var session)
                ? session.Status
                : new ForwardStatus();

            _rows.Add(new GridRow
            {
                Id = config.Id,
                Name = config.Name,
                Server = config.DisplayServer,
                Username = config.Username,
                Auth = config.AuthMode == AuthMode.Password ? "密码" : "私钥",
                Local = config.DisplayLocal,
                Remote = config.DisplayRemote,
                Status = StateText(status.State),
                Message = status.Message
            });
        }

        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();

        if (selectedId is not null)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is GridRow gridRow && gridRow.Id == selectedId)
                {
                    row.Selected = true;
                    break;
                }
            }
        }
    }

    private ForwardConfig? SelectedConfig()
    {
        if (_grid.CurrentRow?.DataBoundItem is not GridRow row)
        {
            return null;
        }

        return _configs.FirstOrDefault(config => config.Id == row.Id);
    }

    private bool IsRunning(Guid id) =>
        _sessions.TryGetValue(id, out var session) && session.IsRunning;

    private void HideToTray()
    {
        Hide();
        _notifyIcon.ShowBalloonTip(1600, "Proxy Forward", "软件已最小化到托盘，转发会继续运行。", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private static string StateText(ForwardState state)
    {
        return state switch
        {
            ForwardState.Starting => "启动中",
            ForwardState.Running => "运行中",
            ForwardState.Failed => "失败",
            _ => "已停止"
        };
    }

    private sealed class GridRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Server { get; set; } = "";
        public string Username { get; set; } = "";
        public string Auth { get; set; } = "";
        public string Local { get; set; } = "";
        public string Remote { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }
}

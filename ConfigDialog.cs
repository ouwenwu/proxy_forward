using System.ComponentModel;

namespace ProxyForward;

public sealed class ConfigDialog : Form
{
    private readonly TextBox _nameBox = new();
    private readonly TextBox _serverHostBox = new();
    private readonly NumericUpDown _sshPortBox = new();
    private readonly TextBox _usernameBox = new();
    private readonly ComboBox _authModeBox = new();
    private readonly TextBox _passwordBox = new();
    private readonly TextBox _privateKeyBox = new();
    private readonly Button _browseKeyButton = new();
    private readonly TextBox _localHostBox = new();
    private readonly NumericUpDown _localPortBox = new();
    private readonly TextBox _remoteHostBox = new();
    private readonly NumericUpDown _remotePortBox = new();
    private readonly CheckBox _proxyAuthEnabledBox = new();
    private readonly TextBox _proxyAuthUsernameBox = new();
    private readonly TextBox _proxyAuthPasswordBox = new();
    private readonly TextBox _notesBox = new();

    private readonly ForwardConfig _original;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ForwardConfig Config { get; private set; }

    public ConfigDialog(ForwardConfig? config = null)
    {
        _original = config is null ? new ForwardConfig() : Clone(config);
        Config = Clone(_original);

        Text = config is null ? "Add forward config" : "Edit forward config";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(600, 700);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildUi();
        LoadConfig();
        UpdateAuthFields();
        UpdateProxyAuthFields();
    }

    private void BuildUi()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 16
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        Controls.Add(table);

        AddRow(table, 0, "Name", _nameBox);
        AddRow(table, 1, "Server host", _serverHostBox);
        ConfigureNumber(_sshPortBox, 1, 65535, 22);
        AddRow(table, 2, "SSH port", _sshPortBox);
        AddRow(table, 3, "SSH username", _usernameBox);

        _authModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _authModeBox.Items.AddRange(["Password", "Private key"]);
        _authModeBox.SelectedIndexChanged += (_, _) => UpdateAuthFields();
        AddRow(table, 4, "SSH auth mode", _authModeBox);

        _passwordBox.UseSystemPasswordChar = true;
        AddRow(table, 5, "SSH password", _passwordBox);

        _browseKeyButton.Text = "Browse...";
        _browseKeyButton.Click += (_, _) => BrowsePrivateKey();
        AddRow(table, 6, "Private key file", _privateKeyBox, _browseKeyButton);

        AddRow(table, 7, "Local proxy host", _localHostBox);
        ConfigureNumber(_localPortBox, 1, 65535, 7897);
        AddRow(table, 8, "Local proxy port", _localPortBox);

        AddRow(table, 9, "Remote bind host", _remoteHostBox);
        ConfigureNumber(_remotePortBox, 1, 65535, 43897);
        AddRow(table, 10, "Remote bind port", _remotePortBox);

        _proxyAuthEnabledBox.Text = "Require HTTP proxy authentication";
        _proxyAuthEnabledBox.CheckedChanged += (_, _) => UpdateProxyAuthFields();
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.Controls.Add(new Label { Text = "Proxy auth", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 11);
        table.Controls.Add(_proxyAuthEnabledBox, 1, 11);
        table.SetColumnSpan(_proxyAuthEnabledBox, 2);

        AddRow(table, 12, "Proxy auth user", _proxyAuthUsernameBox);
        _proxyAuthPasswordBox.UseSystemPasswordChar = true;
        AddRow(table, 13, "Proxy auth password", _proxyAuthPasswordBox);

        _notesBox.Multiline = true;
        _notesBox.ScrollBars = ScrollBars.Vertical;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        table.Controls.Add(new Label { Text = "Notes", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 14);
        table.Controls.Add(_notesBox, 1, 14);
        table.SetColumnSpan(_notesBox, 2);

        var warning = new Label
        {
            Text = "Keep remote bind host as 127.0.0.1 unless you intentionally want to expose the port.",
            ForeColor = Color.DarkRed,
            AutoSize = false,
            Dock = DockStyle.Fill
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        table.Controls.Add(warning, 1, 15);
        table.SetColumnSpan(warning, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 46,
            Padding = new Padding(0, 6, 12, 8)
        };
        Controls.Add(buttons);

        var okButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        okButton.Click += (_, _) =>
        {
            if (!TrySave())
            {
                DialogResult = DialogResult.None;
            }
        };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input, Control? extra = null)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        input.Dock = DockStyle.Fill;
        table.Controls.Add(input, 1, row);

        if (extra is not null)
        {
            extra.Dock = DockStyle.Fill;
            table.Controls.Add(extra, 2, row);
        }
        else
        {
            table.SetColumnSpan(input, 2);
        }
    }

    private static void ConfigureNumber(NumericUpDown input, int min, int max, int value)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
    }

    private void LoadConfig()
    {
        _nameBox.Text = _original.Name;
        _serverHostBox.Text = _original.ServerHost;
        _sshPortBox.Value = Clamp(_original.SshPort);
        _usernameBox.Text = _original.Username;
        _authModeBox.SelectedIndex = _original.AuthMode == AuthMode.Password ? 0 : 1;
        _passwordBox.PlaceholderText = string.IsNullOrEmpty(_original.EncryptedPassword) ? "" : "Leave blank to keep saved password";
        _privateKeyBox.Text = _original.PrivateKeyPath;
        _localHostBox.Text = _original.LocalHost;
        _localPortBox.Value = Clamp(_original.LocalPort);
        _remoteHostBox.Text = _original.RemoteBindHost;
        _remotePortBox.Value = Clamp(_original.RemoteBindPort);
        _proxyAuthEnabledBox.Checked = _original.ProxyAuthEnabled;
        _proxyAuthUsernameBox.Text = _original.ProxyAuthUsername;
        _proxyAuthPasswordBox.PlaceholderText = string.IsNullOrEmpty(_original.EncryptedProxyAuthPassword) ? "" : "Leave blank to keep saved password";
        _notesBox.Text = _original.Notes;
    }

    private bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text) ||
            string.IsNullOrWhiteSpace(_serverHostBox.Text) ||
            string.IsNullOrWhiteSpace(_usernameBox.Text))
        {
            MessageBox.Show("Name, server host, and SSH username are required.", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var authMode = _authModeBox.SelectedIndex == 0 ? AuthMode.Password : AuthMode.PrivateKey;
        if (authMode == AuthMode.Password && string.IsNullOrEmpty(_passwordBox.Text) && string.IsNullOrEmpty(_original.EncryptedPassword))
        {
            MessageBox.Show("SSH password is required.", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (authMode == AuthMode.PrivateKey && string.IsNullOrWhiteSpace(_privateKeyBox.Text))
        {
            MessageBox.Show("Private key file is required.", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_proxyAuthEnabledBox.Checked &&
            (string.IsNullOrWhiteSpace(_proxyAuthUsernameBox.Text) ||
             string.IsNullOrEmpty(_proxyAuthPasswordBox.Text) && string.IsNullOrEmpty(_original.EncryptedProxyAuthPassword)))
        {
            MessageBox.Show("Proxy auth username and password are required.", "Proxy Forward", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        Config = new ForwardConfig
        {
            Id = _original.Id,
            Name = _nameBox.Text.Trim(),
            ServerHost = _serverHostBox.Text.Trim(),
            SshPort = (int)_sshPortBox.Value,
            Username = _usernameBox.Text.Trim(),
            AuthMode = authMode,
            EncryptedPassword = authMode == AuthMode.Password
                ? (string.IsNullOrEmpty(_passwordBox.Text) ? _original.EncryptedPassword : SecretProtector.Protect(_passwordBox.Text))
                : "",
            PrivateKeyPath = authMode == AuthMode.PrivateKey ? _privateKeyBox.Text.Trim() : "",
            LocalHost = string.IsNullOrWhiteSpace(_localHostBox.Text) ? "127.0.0.1" : _localHostBox.Text.Trim(),
            LocalPort = (int)_localPortBox.Value,
            RemoteBindHost = string.IsNullOrWhiteSpace(_remoteHostBox.Text) ? "127.0.0.1" : _remoteHostBox.Text.Trim(),
            RemoteBindPort = (int)_remotePortBox.Value,
            ProxyAuthEnabled = _proxyAuthEnabledBox.Checked,
            ProxyAuthUsername = _proxyAuthEnabledBox.Checked ? _proxyAuthUsernameBox.Text.Trim() : "",
            EncryptedProxyAuthPassword = _proxyAuthEnabledBox.Checked
                ? (string.IsNullOrEmpty(_proxyAuthPasswordBox.Text) ? _original.EncryptedProxyAuthPassword : SecretProtector.Protect(_proxyAuthPasswordBox.Text))
                : "",
            Notes = _notesBox.Text.Trim()
        };

        return true;
    }

    private void UpdateAuthFields()
    {
        var password = _authModeBox.SelectedIndex == 0;
        _passwordBox.Enabled = password;
        _privateKeyBox.Enabled = !password;
        _browseKeyButton.Enabled = !password;
    }

    private void UpdateProxyAuthFields()
    {
        _proxyAuthUsernameBox.Enabled = _proxyAuthEnabledBox.Checked;
        _proxyAuthPasswordBox.Enabled = _proxyAuthEnabledBox.Checked;
    }

    private void BrowsePrivateKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select SSH private key",
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _privateKeyBox.Text = dialog.FileName;
        }
    }

    private static decimal Clamp(int value) => Math.Min(65535, Math.Max(1, value));

    private static ForwardConfig Clone(ForwardConfig config)
    {
        return new ForwardConfig
        {
            Id = config.Id,
            Name = config.Name,
            ServerHost = config.ServerHost,
            SshPort = config.SshPort,
            Username = config.Username,
            AuthMode = config.AuthMode,
            EncryptedPassword = config.EncryptedPassword,
            PrivateKeyPath = config.PrivateKeyPath,
            LocalHost = config.LocalHost,
            LocalPort = config.LocalPort,
            RemoteBindHost = config.RemoteBindHost,
            RemoteBindPort = config.RemoteBindPort,
            ProxyAuthEnabled = config.ProxyAuthEnabled,
            ProxyAuthUsername = config.ProxyAuthUsername,
            EncryptedProxyAuthPassword = config.EncryptedProxyAuthPassword,
            Notes = config.Notes
        };
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SharkTools
{
    /// <summary>
    /// SharkTools 任务窗格用户控件
    /// 这将显示在 SOLIDWORKS 右侧的任务窗格中
    /// </summary>
    [ComVisible(true)]
    [Guid("8A5F5E2D-4B1C-4D3E-9F8A-7C6B5D4E3F2A")]
    [ProgId("SharkTools.TaskPaneControl")]
    public class SharkTaskPaneControl : UserControl
    {
        private Button _helloButton;
        private Button _loginButton;
        private Button _logoutButton;
        private Label _titleLabel;
        private Label _userLabel;
        private TextBox _tokenTextBox;
        private Button _confirmTokenButton;
        private Panel _loginPanel;
        private static ISldWorksProvider _swProvider;

        public interface ISldWorksProvider
        {
            void ShowHello();
            void ShowMessage(string msg);
        }

        public static void SetProvider(ISldWorksProvider provider)
        {
            _swProvider = provider;
        }

        public SharkTaskPaneControl()
        {
            InitializeComponent();
            // 尝试加载已保存的登录状态
            LoadLoginState();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 设置控件大小和背景
            this.Size = new Size(250, 400);
            this.BackColor = Color.White;
            this.Padding = new Padding(10);
            this.AutoScroll = true;

            int yPos = 10;

            // 标题标签
            _titleLabel = new Label
            {
                Text = "🦈 SharkTools",
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 82, 147),
                Location = new Point(10, yPos),
                Size = new Size(230, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_titleLabel);
            yPos += 35;

            // 用户状态标签
            _userLabel = new Label
            {
                Text = "未登录",
                Font = new Font("Microsoft YaHei UI", 9),
                ForeColor = Color.Gray,
                Location = new Point(10, yPos),
                Size = new Size(230, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_userLabel);
            yPos += 25;

            // 分隔线
            Panel separator = new Panel
            {
                BackColor = Color.FromArgb(200, 200, 200),
                Location = new Point(10, yPos),
                Size = new Size(230, 1)
            };
            this.Controls.Add(separator);
            yPos += 10;

            // Hello 按钮
            _helloButton = new Button
            {
                Text = "🦈 打招呼",
                Font = new Font("Microsoft YaHei UI", 10),
                Location = new Point(10, yPos),
                Size = new Size(230, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _helloButton.FlatAppearance.BorderSize = 0;
            _helloButton.Click += HelloButton_Click;
            this.Controls.Add(_helloButton);
            yPos += 50;

            // 登录 GitHub 按钮
            _loginButton = new Button
            {
                Text = "🔗 登录 GitHub",
                Font = new Font("Microsoft YaHei UI", 10),
                Location = new Point(10, yPos),
                Size = new Size(230, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 41, 46),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _loginButton.FlatAppearance.BorderSize = 0;
            _loginButton.Click += LoginButton_Click;
            this.Controls.Add(_loginButton);
            yPos += 50;

            // Token 输入面板（初始隐藏）
            _loginPanel = new Panel
            {
                Location = new Point(10, yPos),
                Size = new Size(230, 80),
                Visible = false
            };
            this.Controls.Add(_loginPanel);

            Label tokenLabel = new Label
            {
                Text = "请输入 GitHub Token:",
                Font = new Font("Microsoft YaHei UI", 9),
                Location = new Point(0, 0),
                Size = new Size(230, 20)
            };
            _loginPanel.Controls.Add(tokenLabel);

            _tokenTextBox = new TextBox
            {
                Location = new Point(0, 22),
                Size = new Size(230, 25),
                Font = new Font("Microsoft YaHei UI", 9),
                UseSystemPasswordChar = true  // 隐藏输入
            };
            _loginPanel.Controls.Add(_tokenTextBox);

            _confirmTokenButton = new Button
            {
                Text = "确认登录",
                Font = new Font("Microsoft YaHei UI", 9),
                Location = new Point(0, 52),
                Size = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _confirmTokenButton.FlatAppearance.BorderSize = 0;
            _confirmTokenButton.Click += ConfirmTokenButton_Click;
            _loginPanel.Controls.Add(_confirmTokenButton);

            Button cancelButton = new Button
            {
                Text = "取消",
                Font = new Font("Microsoft YaHei UI", 9),
                Location = new Point(120, 52),
                Size = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Click += (s, e) => { _loginPanel.Visible = false; };
            _loginPanel.Controls.Add(cancelButton);

            yPos += 90;

            // 退出登录按钮（初始隐藏）
            _logoutButton = new Button
            {
                Text = "退出登录",
                Font = new Font("Microsoft YaHei UI", 9),
                Location = new Point(10, yPos),
                Size = new Size(230, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _logoutButton.FlatAppearance.BorderSize = 0;
            _logoutButton.Click += LogoutButton_Click;
            this.Controls.Add(_logoutButton);

            this.ResumeLayout(false);
        }

        private void LoadLoginState()
        {
            if (GitHubAuth.TryLoadSavedLogin())
            {
                UpdateLoginUI(true);
            }
        }

        private void UpdateLoginUI(bool isLoggedIn)
        {
            if (isLoggedIn && GitHubAuth.IsLoggedIn)
            {
                _userLabel.Text = $"✅ 已登录: {GitHubAuth.GetDisplayName()}";
                _userLabel.ForeColor = Color.FromArgb(40, 167, 69);
                _loginButton.Visible = false;
                _logoutButton.Visible = true;
                _loginPanel.Visible = false;
            }
            else
            {
                _userLabel.Text = "未登录";
                _userLabel.ForeColor = Color.Gray;
                _loginButton.Visible = true;
                _logoutButton.Visible = false;
            }
        }

        private void HelloButton_Click(object sender, EventArgs e)
        {
            _swProvider?.ShowHello();
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            // 显示 Token 输入面板
            _loginPanel.Visible = true;
            _tokenTextBox.Text = "";
            _tokenTextBox.Focus();

            // 同时打开浏览器
            GitHubAuth.StartLogin((success, msg) =>
            {
                // 回调在这里不做太多处理，用户需要手动输入 token
            });
        }

        private async void ConfirmTokenButton_Click(object sender, EventArgs e)
        {
            string token = _tokenTextBox.Text.Trim();
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("请输入 GitHub Token", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _confirmTokenButton.Enabled = false;
            _confirmTokenButton.Text = "登录中...";

            await GitHubAuth.LoginWithToken(token, (success, msg) =>
            {
                // 使用 Invoke 确保在 UI 线程执行
                this.Invoke(new Action(() =>
                {
                    _confirmTokenButton.Enabled = true;
                    _confirmTokenButton.Text = "确认登录";

                    if (success)
                    {
                        UpdateLoginUI(true);
                        MessageBox.Show(msg, "登录成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(msg, "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                GitHubAuth.Logout();
                UpdateLoginUI(false);
                MessageBox.Show("已退出登录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

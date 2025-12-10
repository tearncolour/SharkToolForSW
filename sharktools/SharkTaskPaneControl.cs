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
        private Label _titleLabel;
        private static ISldWorksProvider _swProvider;

        public interface ISldWorksProvider
        {
            void ShowHello();
        }

        public static void SetProvider(ISldWorksProvider provider)
        {
            _swProvider = provider;
        }

        public SharkTaskPaneControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 设置控件大小
            this.Size = new Size(250, 300);
            this.BackColor = Color.White;
            this.Padding = new Padding(10);

            // 标题标签
            _titleLabel = new Label
            {
                Text = "SharkTools",
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 82, 147),
                Location = new Point(10, 10),
                Size = new Size(230, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_titleLabel);

            // 分隔线
            Panel separator = new Panel
            {
                BackColor = Color.FromArgb(200, 200, 200),
                Location = new Point(10, 45),
                Size = new Size(230, 1)
            };
            this.Controls.Add(separator);

            // Hello 按钮（中文）
            _helloButton = new Button
            {
                Text = "🦈 打招呼",
                Font = new Font("Microsoft YaHei UI", 10),
                Location = new Point(10, 60),
                Size = new Size(230, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _helloButton.FlatAppearance.BorderSize = 0;
            _helloButton.Click += HelloButton_Click;
            this.Controls.Add(_helloButton);

            // 说明文字
            Label infoLabel = new Label
            {
                Text = "神奇妙妙小工具\n\n震撼首发！！！爱来自Shark",
                Font = new Font("Microsoft YaHei UI", 9),
                ForeColor = Color.Gray,
                Location = new Point(10, 120),
                Size = new Size(230, 80),
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(infoLabel);

            this.ResumeLayout(false);
        }

        private void HelloButton_Click(object sender, EventArgs e)
        {
            _swProvider?.ShowHello();
        }
    }
}

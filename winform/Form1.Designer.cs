// ─────────────────────────────────────────────────────────────────────────────
// File: Form1.Designer.cs
// ─────────────────────────────────────────────────────────────────────────────
namespace winform
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private Panel panel1;
        private Button button1;
        private Label label1;
        private TextBox textBox1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            panel1 = new Panel();
            button1 = new Button();
            label1 = new Label();
            textBox1 = new TextBox();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Name = "panel1";
            panel1.Dock = DockStyle.Fill;        // ← 폼 전체를 채우도록
            panel1.TabIndex = 0;
            panel1.Paint += panel1_Paint;
            // 
            // button1
            // 
            button1.Location = new Point(9, 8);
            button1.Name = "button1";
            button1.Size = new Size(214, 33);
            button1.TabIndex = 1;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click_1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(9, 44);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 2;
            label1.Text = "label1";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(235, 10);
            textBox1.Margin = new Padding(2);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(799, 23);
            textBox1.TabIndex = 3;
            textBox1.Text = "message";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1795, 849);
            Controls.Add(textBox1);
            Controls.Add(label1);
            Controls.Add(button1);
            Controls.Add(panel1);
            Name = "Form1";
            Text = "Form1";
            // 폼 크기 변경 시 호출될 이벤트
            this.Resize += Form1_Resize;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}

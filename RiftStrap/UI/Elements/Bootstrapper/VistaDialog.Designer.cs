namespace RiftStrap.UI.Elements.Bootstrapper
{
    partial class VistaDialog
    {

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(0, 0);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            Name = "VistaDialog";
            Opacity = 0D;
            ShowInTaskbar = false;
            Text = "VistaDialog";
            WindowState = System.Windows.Forms.FormWindowState.Minimized;
            FormClosing += Dialog_FormClosing;
            Load += Dialog_Load;
            ResumeLayout(false);
        }

    }
}

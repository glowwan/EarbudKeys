// Entry point: single instance, WinForms message loop, tray app.
// C# 5 compatible (in-box csc.exe).
using System;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "EarbudKeys-SingleInstance-7f3a2c91", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new TrayApp());
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show(ex.Message, Strings.T("AppTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }
    }
}

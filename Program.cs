using System;
using System.Windows;

namespace VeriTabaniProjesi
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // WPF uygulaması başlatılır
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}

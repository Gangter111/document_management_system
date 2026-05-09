using System;
using System.Windows;

class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            var app = new Application();
            app.StartupUri = new Uri("DocumentManagement.Wpf/Views/MainWindow.xaml", UriKind.Relative);
            app.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
            Console.WriteLine("Stack: " + ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner: " + ex.InnerException.Message);
            }
        }
    }
}

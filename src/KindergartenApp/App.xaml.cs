using System.Globalization;
using System.Windows;
using KindergartenApp.Services;

namespace KindergartenApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        DispatcherUnhandledException += (_,args) =>
        {
            Messages.Error(args.Exception);
            args.Handled = true;
        };
        ThemeService.Apply(Settings.Current.DarkTheme);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}

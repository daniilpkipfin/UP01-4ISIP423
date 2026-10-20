using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KindergartenApp.Pages;
using KindergartenApp.Services;

namespace KindergartenApp;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        Closing+=OnClosing;
        timer.Tick+=(_,_)=>Clock.Text=DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        timer.Start();
        Clock.Text=DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        Loaded+=(_,_)=>Navigate(new AuthPage());
    }

    public void Navigate(Page page)
    {
        page.Style=(Style)FindResource(typeof(Page));
        MainFrame.Content=page;
        PageTitle.Text=page.Title;
        Title=$"Сеть детских садов | {page.Title}";
        MainFrame.NavigationService.RemoveBackEntry();
        RefreshNavigation();
    }

    public void RefreshNavigation()
    {
        Navigation.Children.Clear();
        Navigation.Visibility=Session.User==null?Visibility.Collapsed:Visibility.Visible;
        if(Session.User==null)
        {
            AccountInfo.Text="УП.01.01 | Попов Д.С. | 4ИСИП-423";
            return;
        }
        Navigation.Children.Add(Ui.Button("Воспитанники",()=>Navigate(new DataPage("Children"))));
        Navigation.Children.Add(Ui.Button("Детские сады",()=>Navigate(new DataPage("Kindergartens"))));
        if(Session.User.IsAdmin)
        {
            Navigation.Children.Add(Ui.Button("Пользователи",()=>Navigate(new DataPage("Users"))));
            Navigation.Children.Add(Ui.Button("Диаграммы",()=>Navigate(new ChartPage())));
        }
        Navigation.Children.Add(Ui.Button("Сменить пароль",()=>Navigate(new ChangePasswordPage())));
        Navigation.Children.Add(Ui.Button("Выйти из аккаунта",Logout));
        AccountInfo.Text=$"{Session.User.FullName} | {Session.User.RoleName}";
    }

    private void Logout()
    {
        if(!Messages.Confirm("Вы действительно хотите выйти из учетной записи?"))return;
        Session.User=null;
        Navigate(new AuthPage());
    }

    private void Theme_Click(object sender,RoutedEventArgs e)=>Messages.Run(()=>ThemeService.Apply(!Settings.Current.DarkTheme));
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(!Messages.Confirm("Вы действительно хотите выйти из приложения?")) e.Cancel=true;
        else timer.Stop();
    }

}

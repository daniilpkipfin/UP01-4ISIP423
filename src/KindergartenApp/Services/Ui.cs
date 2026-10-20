using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KindergartenApp.Services;

public static class Ui
{
    public static Button Button(string text, Action action, string? hint=null)
    {
        var button=new Button { Content=text, ToolTip=hint??text };
        AutomationProperties.SetName(button,text);
        button.Click+=(_,_)=>Messages.Run(action);
        return button;
    }

    public static TextBox Text(string hint,int max=150,string value="")
    {
        var box=new TextBox { Text=value,Tag=hint,ToolTip=hint,MaxLength=max };
        AutomationProperties.SetName(box,hint);
        return box;
    }

    public static PasswordBox Password(string hint="Пароль")
    {
        var box=new PasswordBox { MaxLength=128,ToolTip=hint,Tag=hint };
        AutomationProperties.SetName(box,hint);
        return box;
    }

    public static void Field(Panel panel,string label,FrameworkElement input)
    {
        panel.Children.Add(new TextBlock { Text=label,Margin=new Thickness(0,10,0,5) });
        if(input is PasswordBox password)
        {
            var grid=new Grid();
            var hint=new TextBlock { Text=password.Tag?.ToString(),Margin=new Thickness(11,6,0,0),IsHitTestVisible=false };
            hint.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
            grid.Children.Add(password); grid.Children.Add(hint);
            password.PasswordChanged+=(_,_)=>hint.Visibility=password.Password.Length==0?Visibility.Visible:Visibility.Collapsed;
            panel.Children.Add(grid);
        }
        else panel.Children.Add(input);
    }

    public static StackPanel Form(Page page,string title,int width=480)
    {
        page.Title=title;
        var panel=new StackPanel { MaxWidth=width,Margin=new Thickness(24),HorizontalAlignment=HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text=title,Style=(Style)Application.Current.FindResource("Heading") });
        page.Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        return panel;
    }

    public static WrapPanel Actions(Panel panel)
    {
        var actions=new WrapPanel { Margin=new Thickness(0,14,0,0) };
        panel.Children.Add(actions);
        return actions;
    }

    public static ComboBox Combo(IEnumerable<string> items,string hint)
    {
        var box=new ComboBox { ItemsSource=items,SelectedIndex=0,ToolTip=hint };
        AutomationProperties.SetName(box,hint);
        return box;
    }

    public static void Digits(TextBox input,bool phone=false,bool money=false)
    {
        input.PreviewTextInput+=(_,e)=>e.Handled=e.Text.Any(c=>!char.IsDigit(c)&&!(phone&&c=='+')&&!(money&&(c==','||c=='.')));
        DataObject.AddPastingHandler(input,(_,e)=>
        {
            var text=e.DataObject.GetData(DataFormats.UnicodeText) as string;
            if(text==null||text.Any(c=>!char.IsDigit(c)&&!(phone&&c=='+')&&!(money&&(c==','||c=='.'))))e.CancelCommand();
        });
    }

    public static BitmapImage Bitmap(byte[] bytes)
    {
        using var stream=new MemoryStream(bytes);
        var image=new BitmapImage(); image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }

    public static MainWindow Main => (MainWindow)Application.Current.MainWindow;
}

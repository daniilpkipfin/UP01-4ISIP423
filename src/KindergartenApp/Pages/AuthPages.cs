using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Kindergarten.Core;
using Validation = Kindergarten.Core.Validation;
using KindergartenApp.Services;

namespace KindergartenApp.Pages;

public sealed class CaptchaView : StackPanel
{
    private readonly Canvas canvas=new() { Width=300,Height=72,Background=Brushes.White,ClipToBounds=true };
    private readonly TextBox answer=Ui.Text("Введите шесть символов с картинки",6);
    public string Answer=>answer.Text;

    public CaptchaView()
    {
        Children.Add(new TextBlock { Text="Проверка после трех неверных попыток",Margin=new Thickness(0,12,0,6) });
        Children.Add(canvas);
        Ui.Field(this,"Код с картинки",answer);
        Children.Add(Ui.Button("Обновить код",()=>{Session.Attempts.Refresh();Refresh();},"Создать новое изображение капчи"));
        Refresh();
    }

    public void Refresh()
    {
        Visibility=Session.Attempts.CaptchaRequired?Visibility.Visible:Visibility.Collapsed;
        answer.Clear();canvas.Children.Clear();
        for(var i=0;i<8;i++)canvas.Children.Add(new Line { X1=RandomNumberGenerator.GetInt32(300),Y1=RandomNumberGenerator.GetInt32(72),X2=RandomNumberGenerator.GetInt32(300),Y2=RandomNumberGenerator.GetInt32(72),Stroke=Brushes.LightSlateGray,StrokeThickness=1 });
        for(var i=0;i<Session.Attempts.Challenge.Length;i++)
        {
            var text=new TextBlock { Text=Session.Attempts.Challenge[i].ToString(),FontSize=30,FontWeight=FontWeights.Bold,Foreground=Brushes.DarkBlue,RenderTransform=new RotateTransform(RandomNumberGenerator.GetInt32(-15,16)),IsHitTestVisible=false };
            Canvas.SetLeft(text,20+i*43);Canvas.SetTop(text,14+RandomNumberGenerator.GetInt32(7));canvas.Children.Add(text);
        }
    }

    public bool Validate()
    {
        if(Session.Attempts.Check(Answer)) return true;
        Session.Attempts.Fail();Refresh();Messages.Info("Код капчи неверен. Введите новый код.");return false;
    }
}

public sealed class AuthPage : Page
{
    public AuthPage()
    {
        var form=Ui.Form(this,"Авторизация");
        form.Children.Add(new TextBlock { Text="Учет детских садов и воспитанников. Войдите под своей учетной записью." });
        var login=Ui.Text("Логин",50);
        var password=Ui.Password();
        Ui.Field(form,"Логин",login);Ui.Field(form,"Пароль",password);
        var captcha=new CaptchaView();form.Children.Add(captcha);
        var actions=Ui.Actions(form);
        var enter=Ui.Button("Войти",()=>
        {
            if(string.IsNullOrWhiteSpace(login.Text)||password.Password.Length==0)throw new DomainException("Введите логин и пароль.");
            if(!captcha.Validate())return;
            var user=Session.Repository.Authenticate(login.Text,password.Password);
            if(user==null)
            {
                Session.Attempts.Fail();captcha.Refresh();password.Clear();
                Messages.Info("Логин или пароль неверен.");return;
            }
            Session.Attempts.Success();Session.User=user;
            Ui.Main.Navigate(new DataPage("Children"));
        });
        enter.IsDefault=true;
        actions.Children.Add(enter);
        actions.Children.Add(Ui.Button("Регистрация",()=>Ui.Main.Navigate(new RegisterPage())));
        actions.Children.Add(Ui.Button("Сменить пароль",()=>Ui.Main.Navigate(new ChangePasswordPage())));
    }
}

public sealed class RegisterPage : Page
{
    public RegisterPage()
    {
        var form=Ui.Form(this,"Регистрация пользователя");
        var login=Ui.Text("От 3 до 50 латинских символов",50);
        var name=Ui.Text("Фамилия Имя Отчество",150);
        var password=Ui.Password("От 6 символов, латинские буквы и цифры");
        var confirm=Ui.Password("Повторите пароль");
        Ui.Field(form,"Логин",login);Ui.Field(form,"ФИО",name);Ui.Field(form,"Пароль",password);Ui.Field(form,"Подтверждение пароля",confirm);
        var role=Ui.Combo(["Пользователь"],"Роль новой учетной записи");role.IsEnabled=false;
        Ui.Field(form,"Роль",role);
        form.Children.Add(new TextBlock { Text="Администратора может создать только действующий администратор.",Margin=new Thickness(0,12,0,0) });
        var actions=Ui.Actions(form);
        actions.Children.Add(Ui.Button("Зарегистрировать",()=>
        {
            Session.Repository.Register(login.Text,name.Text,password.Password,confirm.Password);
            Messages.Info("Регистрация завершена. Теперь выполните вход.");Ui.Main.Navigate(new AuthPage());
        }));
        actions.Children.Add(Ui.Button("Назад",()=>Ui.Main.Navigate(new AuthPage())));
    }
}

public sealed class ChangePasswordPage : Page
{
    public ChangePasswordPage()
    {
        var form=Ui.Form(this,"Смена пароля");
        var login=Ui.Text("Логин",50,Session.User?.Login??"");login.IsReadOnly=Session.User!=null;
        var old=Ui.Password("Текущий пароль");var password=Ui.Password("Новый пароль");var confirm=Ui.Password("Повторите новый пароль");
        Ui.Field(form,"Логин",login);Ui.Field(form,"Текущий пароль",old);Ui.Field(form,"Новый пароль",password);Ui.Field(form,"Подтверждение",confirm);
        var captcha=new CaptchaView();form.Children.Add(captcha);
        var actions=Ui.Actions(form);
        actions.Children.Add(Ui.Button("Сохранить",()=>
        {
            if(string.IsNullOrWhiteSpace(login.Text)||old.Password.Length==0)throw new DomainException("Введите логин и текущий пароль.");
            Validation.Password(password.Password,confirm.Password);
            if(!captcha.Validate())return;
            var user=Session.Repository.Authenticate(login.Text,old.Password);
            if(user==null)
            {
                Session.Attempts.Fail();captcha.Refresh();Messages.Info("Логин или текущий пароль неверен.");return;
            }
            Session.Repository.ChangePassword(user.Id,old.Password,password.Password,confirm.Password);
            Session.Attempts.Success();Session.User=null;
            Messages.Info("Пароль изменен. Выполните вход с новым паролем.");Ui.Main.Navigate(new AuthPage());
        }));
        actions.Children.Add(Ui.Button("Назад",()=>Ui.Main.Navigate(Session.User==null?new AuthPage():new DataPage("Children"))));
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Kindergarten.Core;
using Validation = Kindergarten.Core.Validation;
using KindergartenApp.Services;
using Microsoft.Win32;

namespace KindergartenApp.Pages;

public sealed class EditPage : Page
{
    public EditPage(string entity,object? row)
    {
        if(!Session.Repository.GetAccount(Session.Id).IsAdmin)throw new DomainException("Редактирование доступно только администратору.");
        var subject=entity switch {"Users"=>"пользователя","Kindergartens"=>"детского сада",_=>"воспитанника"};
        var panel=Ui.Form(this,$"{(row==null?"Добавление":"Редактирование")} {subject}",640);
        Action save;
        if(entity=="Users")
        {
            var user=row as Account;
            var login=Ui.Text("Логин",50,user?.Login??"");var name=Ui.Text("Фамилия Имя Отчество",150,user?.FullName??"");
            var role=Ui.Combo(["Пользователь","Администратор"],"Роль");role.SelectedIndex=user?.IsAdmin==true?1:0;
            var password=Ui.Password(user==null?"Пароль":"Оставьте пустым, чтобы сохранить пароль");
            var confirmation=Ui.Password("Подтверждение нового пароля");
            Ui.Field(panel,"Логин",login);Ui.Field(panel,"ФИО",name);Ui.Field(panel,"Роль",role);Ui.Field(panel,"Новый пароль",password);Ui.Field(panel,"Подтверждение пароля",confirmation);
            byte[]? photo=user?.Photo;
            var image=new Image { Height=80,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8) };
            if(photo!=null)image.Source=Ui.Bitmap(photo);
            panel.Children.Add(image);
            var photos=new WrapPanel();panel.Children.Add(photos);
            photos.Children.Add(Ui.Button("Выбрать фото",()=>
            {
                var dialog=new OpenFileDialog { Filter="Изображения PNG и JPEG|*.png;*.jpg;*.jpeg" };
                if(dialog.ShowDialog()!=true)return;
                if(new FileInfo(dialog.FileName).Length>2097152)throw new DomainException("Файл должен быть не больше 2 МБ.");
                var bytes=File.ReadAllBytes(dialog.FileName);var bitmap=Ui.Bitmap(bytes);photo=bytes;image.Source=bitmap;
            }));
            photos.Children.Add(Ui.Button("Убрать фото",()=>{photo=null;image.Source=null;}));
            save=()=>
            {
                if(user==null||password.Password.Length>0||confirmation.Password.Length>0)Validation.Password(password.Password,confirmation.Password);
                Session.Repository.SaveUser(Session.Id,user?.Id??0,login.Text,name.Text,role.SelectedIndex==1?"Admin":"User",password.Password,photo);
                Session.User=Session.Repository.GetAccount(Session.Id);
            };
        }
        else if(entity=="Kindergartens")
        {
            var garden=row as Garden;
            var name=Ui.Text("Название детского сада",100,garden?.Name??"");var address=Ui.Text("Адрес",200,garden?.Address??"");
            var phone=Ui.Text("+74951234567",12,garden?.Phone??"");Ui.Digits(phone,phone:true);
            var capacity=Ui.Text("От 1 до 500",3,garden?.Capacity.ToString()??"30");Ui.Digits(capacity);
            Ui.Field(panel,"Название",name);Ui.Field(panel,"Адрес",address);Ui.Field(panel,"Телефон",phone);Ui.Field(panel,"Вместимость",capacity);
            save=()=>
            {
                if(!int.TryParse(capacity.Text,out var number))throw new DomainException("Вместимость должна быть целым числом от 1 до 500.");
                Session.Repository.SaveGarden(Session.Id,garden?.Id??0,name.Text,address.Text,phone.Text,number);
            };
        }
        else
        {
            var child=row as Child;
            var name=Ui.Text("Фамилия Имя Отчество",150,child?.FullName??"");
            var birth=new DatePicker { SelectedDate=child?.BirthDate??DateTime.Today.AddYears(-4),DisplayDateStart=DateTime.Today.AddYears(-10),DisplayDateEnd=DateTime.Today,ToolTip="Дата рождения, возраст 0-10 лет" };
            var enrollment=new DatePicker { SelectedDate=child?.EnrollmentDate??DateTime.Today,DisplayDateEnd=DateTime.Today,ToolTip="Дата зачисления" };
            var gardens=new ComboBox { ItemsSource=Session.Repository.Gardens(Session.Id),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=child?.GardenId,ToolTip="Детский сад" };
            var parents=new ComboBox { ItemsSource=Session.Repository.Users(Session.Id),DisplayMemberPath="FullName",SelectedValuePath="Id",SelectedValue=child?.ParentId,ToolTip="Родитель или законный представитель" };
            var group=Ui.Text("Например Старшая",50,child?.GroupName??"");
            var status=Ui.Combo(Validation.Statuses,"Статус");status.SelectedItem=child?.Status??"Зачислен";
            var fee=Ui.Text("От 0 до 100000 рублей",9,child?.MonthlyFee.ToString("F2")??"0,00");Ui.Digits(fee,money:true);
            Ui.Field(panel,"ФИО воспитанника",name);Ui.Field(panel,"Дата рождения",birth);Ui.Field(panel,"Дата зачисления",enrollment);
            Ui.Field(panel,"Детский сад",gardens);Ui.Field(panel,"Родитель",parents);Ui.Field(panel,"Группа",group);Ui.Field(panel,"Статус",status);Ui.Field(panel,"Ежемесячная плата, руб.",fee);
            save=()=>
            {
                if(gardens.SelectedValue is not int gardenId||parents.SelectedValue is not int parentId)throw new DomainException("Выберите детский сад и родителя.");
                if(birth.SelectedDate==null||enrollment.SelectedDate==null)throw new DomainException("Укажите обе даты.");
                if(!decimal.TryParse(fee.Text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount))throw new DomainException("Введите корректную сумму платы.");
                Session.Repository.SaveChild(Session.Id,child?.Id??0,gardenId,parentId,name.Text,birth.SelectedDate.Value,enrollment.SelectedDate.Value,group.Text,status.Text,amount);
            };
        }
        var actions=Ui.Actions(panel);
        actions.Children.Add(Ui.Button("Сохранить",()=>{save();Messages.Info("Данные сохранены.");Ui.Main.Navigate(new DataPage(entity));}));
        actions.Children.Add(Ui.Button("Отменить",()=>Ui.Main.Navigate(new DataPage(entity))));
    }
}

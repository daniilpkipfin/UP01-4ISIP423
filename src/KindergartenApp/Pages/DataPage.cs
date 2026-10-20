using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kindergarten.Core;
using Validation = Kindergarten.Core.Validation;
using KindergartenApp.Services;
using Microsoft.Win32;

namespace KindergartenApp.Pages;

public sealed class DataPage : Page
{
    private readonly string entity;
    private readonly DataGrid grid=new();
    private readonly TextBox search=Ui.Text("Поиск по тексту",150);
    private readonly ComboBox filter=Ui.Combo(["Все"],"Фильтр по статусу или роли");
    private readonly ComboBox garden=Ui.Combo(["Все детские сады"],"Фильтр по детскому саду");
    private readonly ComboBox sort=Ui.Combo(["По возрастанию","По убыванию"],"Сортировка по названию или ФИО");
    private readonly TextBlock count=new() { Margin=new Thickness(0,8,0,8) };
    private List<object> source=[];

    public DataPage(string name)
    {
        entity=name;
        var admin=Session.Repository.GetAccount(Session.Id).IsAdmin;
        if(entity=="Users"&&!admin)throw new DomainException("Раздел доступен только администратору.");
        Title=entity switch { "Users"=>"Пользователи", "Kindergartens"=>"Детские сады", _=>admin?"Воспитанники":"Мои воспитанники" };
        var layout=new DockPanel { Margin=new Thickness(20) };
        Content=layout;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        top.Children.Add(new TextBlock { Text=Title,Style=(Style)Application.Current.FindResource("Heading") });
        var filters=new WrapPanel();top.Children.Add(filters);
        search.Width=250;search.Margin=new Thickness(0,0,10,8);filters.Children.Add(search);
        filter.Width=165;filter.Margin=new Thickness(0,0,10,8);filters.Children.Add(filter);
        garden.Width=210;garden.Margin=new Thickness(0,0,10,8);filters.Children.Add(garden);
        sort.Width=175;sort.Margin=new Thickness(0,0,10,8);filters.Children.Add(sort);
        filters.Children.Add(Ui.Button("Сбросить фильтры",()=>{search.Clear();filter.SelectedIndex=0;garden.SelectedIndex=0;sort.SelectedIndex=0;Update();}));
        if(entity=="Children")
        {
            filter.ItemsSource=new[]{"Все статусы"}.Concat(Validation.Statuses);
            garden.ItemsSource=new[]{"Все детские сады"}.Concat(Session.Repository.Gardens(Session.Id).Select(x=>x.Name).Distinct());
            Column("ID","Id",65);Column("ФИО воспитанника","FullName",240);Column("Рождение","BirthText",110);
            Column("Детский сад","GardenName",140);Column("Группа","GroupName",160);Column("Статус","Status",120);
            Column("Плата, руб.","MonthlyFee",120);Column("Родитель","ParentName",240);Column("Зачисление","EnrollmentText",120);
        }
        else if(entity=="Kindergartens")
        {
            filter.ItemsSource=new[]{"Все сады","Есть места","Нет мест"};garden.Visibility=Visibility.Collapsed;
            Column("ID","Id",65);Column("Название","Name",200);Column("Адрес","Address",260);Column("Телефон","Phone",150);
            Column("Вместимость","Capacity",135);Column("Зачислено","Enrolled",125);Column("Свободно","Available",120);
        }
        else
        {
            filter.ItemsSource=new[]{"Все роли","Администратор","Пользователь"};garden.Visibility=Visibility.Collapsed;
            Column("ID","Id",65);Column("Логин","Login",180);Column("ФИО","FullName",360);Column("Роль","RoleName",200);
        }
        filter.SelectedIndex=0;garden.SelectedIndex=0;top.Children.Add(count);
        var bottom=new WrapPanel { Margin=new Thickness(0,10,0,0) };DockPanel.SetDock(bottom,Dock.Bottom);layout.Children.Add(bottom);
        if(admin)
        {
            bottom.Children.Add(Ui.Button("Добавить",()=>Ui.Main.Navigate(new EditPage(entity,null))));
            bottom.Children.Add(Ui.Button("Редактировать",()=>Ui.Main.Navigate(new EditPage(entity,Selected()))));
            bottom.Children.Add(Ui.Button("Удалить",Delete));
            var format=Ui.Combo(["DOCX","PDF","XLSX"],"Формат отчета");format.Width=100;format.Margin=new Thickness(0,4,8,4);bottom.Children.Add(format);
            bottom.Children.Add(Ui.Button("Экспорт",()=>Export(format.SelectedItem?.ToString()??"DOCX"),"Экспортировать видимые строки с учетом фильтров и сортировки"));
        }
        bottom.Children.Add(Ui.Button("Обновить",Load));
        layout.Children.Add(grid);
        search.TextChanged+=(_,_)=>Update();filter.SelectionChanged+=(_,_)=>Update();garden.SelectionChanged+=(_,_)=>Update();sort.SelectionChanged+=(_,_)=>Update();
        Load();
    }

    private void Column(string title,string property,int width)=>grid.Columns.Add(new DataGridTextColumn { Header=title,Binding=new Binding(property),Width=width });
    private object Selected()=>grid.SelectedItem??throw new DomainException("Сначала выберите запись в таблице.");
    private static int Id(object row)=>row switch { Account x=>x.Id,Garden x=>x.Id,Child x=>x.Id,_=>0 };
    private static string RowName(object row)=>row switch { Account x=>x.FullName,Garden x=>x.Name,Child x=>x.FullName,_=>"" };

    private void Load()
    {
        source=entity switch
        {
            "Users"=>Session.Repository.Users(Session.Id).Cast<object>().ToList(),
            "Kindergartens"=>Session.Repository.Gardens(Session.Id).Cast<object>().ToList(),
            _=>Session.Repository.Children(Session.Id).Cast<object>().ToList()
        };
        Update();
    }

    private void Update()
    {
        var rows=source.Where(row=>string.Join(" ",Values(row)).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase));
        rows=rows.Where(row=>row switch
        {
            Account a=>filter.SelectedIndex<=0||a.RoleName==(string?)filter.SelectedItem,
            Garden g=>filter.SelectedIndex<=0||(filter.SelectedIndex==1?g.Available>0:g.Available==0),
            Child c=>(filter.SelectedIndex<=0||c.Status==(string?)filter.SelectedItem)&&(garden.SelectedIndex<=0||c.GardenName==(string?)garden.SelectedItem),
            _=>true
        });
        grid.ItemsSource=(sort.SelectedIndex==1?rows.OrderByDescending(RowName):rows.OrderBy(RowName)).ToList();
        count.Text=$"Показано: {grid.Items.Count} из {source.Count}. Сортировка также доступна по заголовкам столбцов.";
    }

    private void Delete()
    {
        var row=Selected();var id=Id(row);
        var dependent=entity=="Children"?0:Session.Repository.DependentChildren(Session.Id,entity,id);
        var message=$"Удалить запись «{RowName(row)}»?";
        if(entity!="Children")message+=$" Все связанные записи воспитанников будут удалены каскадно. Сейчас связанных записей: {dependent}.";
        if(!Messages.Confirm(message))return;
        Session.Repository.Delete(Session.Id,entity,id);Load();Messages.Info("Запись удалена.");
    }

    private string[] Values(object row)=>row switch
    {
        Account a=>[a.Id.ToString(),a.Login,a.FullName,a.RoleName],
        Garden g=>[g.Id.ToString(),g.Name,g.Address,g.Phone,g.Capacity.ToString(),g.Enrolled.ToString(),g.Available.ToString()],
        Child c=>[c.Id.ToString(),c.FullName,c.BirthText,c.GardenName,c.GroupName,c.Status,c.MonthlyFee.ToString("F2"),c.ParentName,c.EnrollmentText],
        _=>[]
    };

    private void Export(string format)
    {
        if(!Session.Repository.GetAccount(Session.Id).IsAdmin)throw new DomainException("Экспорт доступен только администратору.");
        var report=new ReportData(Title,grid.Columns.Select(x=>x.Header.ToString()??"").ToArray(),grid.Items.Cast<object>().Select(Values).ToList(),
            $"Поиск: {search.Text}; фильтр: {filter.Text}; детский сад: {(garden.Visibility==Visibility.Visible?garden.Text:"все")}");
        var extension=format.ToLowerInvariant();
        var dialog=new SaveFileDialog { FileName=$"{entity}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}",Filter=$"{format}|*.{extension}",DefaultExt=extension,AddExtension=true };
        if(dialog.ShowDialog()!=true)return;
        ExportService.Save(dialog.FileName,format,report);
        Messages.Info($"Отчет сохранен: {dialog.FileName}");
    }
}

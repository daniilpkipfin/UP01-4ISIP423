using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kindergarten.Core;
using KindergartenApp.Services;
using Microsoft.Win32;

namespace KindergartenApp.Pages;

public sealed class ChartPage : Page
{
    private readonly StackPanel chart=new();
    private readonly ComboBox metric=Ui.Combo(["Зачисленные воспитанники","Плановая плата за месяц"],"Показатель диаграммы");
    private readonly ComboBox garden=Ui.Combo(["Все детские сады"],"Выбрать детский сад");
    private ReportData current=new("Диаграмма",[],[],"");

    public ChartPage()
    {
        if(!Session.Repository.GetAccount(Session.Id).IsAdmin)throw new DomainException("Диаграммы доступны только администратору.");
        var panel=Ui.Form(this,"Диаграммы по детским садам",1000);
        var filters=new WrapPanel();panel.Children.Add(filters);
        metric.Width=280;metric.Margin=new Thickness(0,0,12,12);filters.Children.Add(metric);
        garden.Width=240;garden.Margin=new Thickness(0,0,12,12);filters.Children.Add(garden);
        garden.ItemsSource=new[]{"Все детские сады"}.Concat(Session.Repository.Gardens(Session.Id).Select(x=>x.Name).Distinct());garden.SelectedIndex=0;
        panel.Children.Add(new TextBlock { Text="Учитываются только записи со статусом «Зачислен». Плановая плата не является суммой фактически полученных платежей.",Margin=new Thickness(0,0,0,20) });
        panel.Children.Add(chart);
        var actions=Ui.Actions(panel);
        actions.Children.Add(Ui.Button("Обновить",Update));
        foreach(var format in new[]{"DOCX","PDF","XLSX"})actions.Children.Add(Ui.Button($"Экспорт {format}",()=>
        {
            if(!Session.Repository.GetAccount(Session.Id).IsAdmin)throw new DomainException("Необходима роль администратора.");
            var extension=format.ToLowerInvariant();
            var dialog=new SaveFileDialog { FileName=$"Diagram_{DateTime.Now:yyyyMMdd}.{extension}",Filter=$"{format}|*.{extension}",AddExtension=true,DefaultExt=extension };
            if(dialog.ShowDialog()!=true)return;
            ExportService.Save(dialog.FileName,format,current);Messages.Info("Данные диаграммы сохранены.");
        }));
        metric.SelectionChanged+=(_,_)=>Update();garden.SelectionChanged+=(_,_)=>Update();Update();
    }

    private void Update()
    {
        if(!Session.Repository.GetAccount(Session.Id).IsAdmin)throw new DomainException("Необходима роль администратора.");
        chart.Children.Clear();
        var children=Session.Repository.Children(Session.Id).Where(x=>x.Status=="Зачислен").ToList();
        var gardens=Session.Repository.Gardens(Session.Id).Where(x=>garden.SelectedIndex<=0||x.Name==garden.Text).ToList();
        var values=gardens.Select(g=>(Garden:g,Value:metric.SelectedIndex==0?(decimal)children.Count(c=>c.GardenId==g.Id):children.Where(c=>c.GardenId==g.Id).Sum(c=>c.MonthlyFee))).ToList();
        var maximum=Math.Max(1,values.Count==0?0:values.Max(x=>x.Value));
        foreach(var item in values)
        {
            var row=new Grid { Margin=new Thickness(0,12,0,12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(180) });row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(500) });row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(140) });
            row.Children.Add(new TextBlock { Text=item.Garden.Name,VerticalAlignment=VerticalAlignment.Center });
            var bar=new Border { Height=36,Width=(double)(item.Value/maximum)*480,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(3) };bar.SetResourceReference(Border.BackgroundProperty,"AccentBrush");Grid.SetColumn(bar,1);row.Children.Add(bar);
            var label=new TextBlock { Text=metric.SelectedIndex==0?item.Value.ToString("0"):item.Value.ToString("N2")+" руб.",VerticalAlignment=VerticalAlignment.Center };Grid.SetColumn(label,2);row.Children.Add(label);chart.Children.Add(row);
        }
        if(values.Count==0)chart.Children.Add(new TextBlock { Text="Нет данных для диаграммы." });
        chart.Children.Add(new TextBlock { Text=$"Итого: {values.Sum(x=>x.Value):N2}{(metric.SelectedIndex==0?" воспитанников":" руб.")}",FontWeight=FontWeights.Bold,Margin=new Thickness(0,16,0,16) });
        current=new ReportData(metric.Text,["Детский сад",metric.SelectedIndex==0?"Количество":"Плата, руб."],values.Select(x=>new[]{x.Garden.Name,x.Value.ToString(metric.SelectedIndex==0?"0":"F2")}).ToList(),$"Статус: Зачислен; {garden.Text}");
    }
}

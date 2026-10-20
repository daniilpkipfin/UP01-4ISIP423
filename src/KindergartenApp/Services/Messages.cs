using System.IO;
using System.Windows;
using Kindergarten.Core;
using Microsoft.Data.SqlClient;

namespace KindergartenApp.Services;

public static class Messages
{
    public static void Info(string message) => MessageBox.Show(Application.Current.MainWindow,message,"Сеть детских садов",MessageBoxButton.OK,MessageBoxImage.Information);
    public static bool Confirm(string message) => MessageBox.Show(Application.Current.MainWindow,message,"Подтверждение",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;

    public static void Error(Exception error)
    {
        Directory.CreateDirectory(Settings.Folder);
        try { File.AppendAllText(Path.Combine(Settings.Folder,"errors.log"),$"{DateTime.Now:O} {error.GetType().Name}: {error.Message}\n"); }
        catch (IOException) { }
        var text=error switch
        {
            DomainException => error.Message,
            SqlException sql when sql.Number is 2601 or 2627 => "Такая запись уже существует. Проверьте логин, название или ФИО воспитанника.",
            SqlException sql when sql.Number == 547 => "Данные нарушают ограничения базы. Проверьте значения и связанные записи.",
            SqlException sql when sql.Number == 1205 => "Данные изменяются другим пользователем. Повторите действие.",
            SqlException => "Не удалось выполнить запрос SQL Server. Проверьте сервер и наличие базы. Подробности записаны в журнал ошибок.",
            IOException or UnauthorizedAccessException => "Не удалось прочитать или сохранить файл. Проверьте доступ к папке и закройте открытый файл.",
            _ => "Операция не выполнена. Подробности записаны в журнал ошибок."
        };
        MessageBox.Show(Application.Current.MainWindow,text,"Ошибка",MessageBoxButton.OK,MessageBoxImage.Warning);
    }

    public static void Run(Action action)
    {
        try { action(); }
        catch(Exception e) { Error(e); }
    }
}

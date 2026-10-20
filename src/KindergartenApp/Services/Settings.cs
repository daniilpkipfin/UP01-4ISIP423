using System.IO;
using System.Text.Json;
using System.Windows;
using Kindergarten.Core;

namespace KindergartenApp.Services;

public sealed class Settings
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PopovKindergartens");
    private static string FileName => Path.Combine(Folder, "settings.json");
    public static Settings Current { get; } = Load();
    public string Server { get; set; } = @"localhost\SQLEXPRESS";
    public bool DarkTheme { get; set; }

    private static Settings Load()
    {
        try { return File.Exists(FileName) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? new() : new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName, JsonSerializer.Serialize(this));
    }
}

public static class ThemeService
{
    public static void Apply(bool dark)
    {
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(dark ? "Styles/PopovTheme.xaml" : "Styles/Dictionary.xaml", UriKind.Relative)
        };
        Settings.Current.DarkTheme = dark;
        Settings.Current.Save();
    }
}

public static class Session
{
    public static Account? User { get; set; }
    public static int Id => User?.Id ?? throw new DomainException("Необходимо выполнить вход.");
    public static Database Database => new(Database.LocalConnection(Settings.Current.Server));
    public static Repository Repository => new(Database);
    public static AttemptGuard Attempts { get; } = new(Path.Combine(Settings.Folder,"attempts.txt"));
}

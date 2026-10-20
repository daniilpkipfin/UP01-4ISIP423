using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Kindergarten.Core;

public sealed class Database(string connectionString)
{
    public const string Name = "DB_Popov_Kindergartens";
    public string ConnectionString { get; } = connectionString;

    public static string LocalConnection(string server)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server.Trim(), InitialCatalog = Name, IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 8,
            ApplicationName = "KindergartenNetwork"
        };
        return builder.ConnectionString;
    }

    public SqlConnection Open()
    {
        var connection = new SqlConnection(ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    public void Check()
    {
        using var connection = Open();
        using var command = Command(connection, null,
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Users','Kindergartens','Children')");
        if (Convert.ToInt32(command.ExecuteScalar()) != 3)
            throw new DomainException("В базе отсутствуют таблицы проекта. Выполните создание учебной базы.");
        using var schema = Command(connection, null,
            "SELECT TOP (0) u.PasswordHash, u.Photo, k.Capacity, c.MonthlyFee FROM dbo.Users u, dbo.Kindergartens k, dbo.Children c");
        schema.ExecuteNonQuery();
    }

    public void Initialize()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Kindergarten.Core.schema.sql")
            ?? throw new DomainException("Не найден встроенный скрипт базы данных.");
        using var reader = new StreamReader(stream);
        foreach (var batch in Regex.Split(reader.ReadToEnd(), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            using var command = Command(connection, null, batch);
            command.CommandTimeout = 60;
            command.ExecuteNonQuery();
        }
        Check();
    }

    public static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string text,
        params (string Name, object? Value)[] values)
    {
        var command = new SqlCommand(text, connection, transaction);
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}

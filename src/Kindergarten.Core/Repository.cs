using System.Data;
using Microsoft.Data.SqlClient;

namespace Kindergarten.Core;

public sealed class Repository(Database database)
{
    private static Account ReadAccount(SqlDataReader r) => new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : (byte[])r[4]);

    public Account? Authenticate(string login, string password)
    {
        using var connection = database.Open();
        using var command = Database.Command(connection, null,
            "SELECT Id, Login, FullName, Role, Photo, PasswordHash FROM dbo.Users WHERE Login=@login", ("@login", login.Trim()));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            PasswordHasher.Hash(password);
            return null;
        }
        return PasswordHasher.Verify(password, reader.GetString(5)) ? ReadAccount(reader) : null;
    }

    public Account GetAccount(int id)
    {
        using var connection = database.Open();
        return Require(connection, null, id, false);
    }

    private static Account Require(SqlConnection connection, SqlTransaction? transaction, int id, bool admin)
    {
        using var command = Database.Command(connection, transaction,
            "SELECT Id, Login, FullName, Role, Photo FROM dbo.Users WHERE Id=@id", ("@id", id));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new DomainException("Учетная запись недоступна. Выполните вход заново.");
        var account = ReadAccount(reader);
        if (admin && !account.IsAdmin) throw new DomainException("Действие доступно только администратору.");
        return account;
    }

    public int Register(string login, string fullName, string password, string confirmation)
    {
        login = Validation.Login(login);
        fullName = Validation.FullName(fullName);
        Validation.Password(password, confirmation);
        using var connection = database.Open();
        using var command = Database.Command(connection, null,
            "INSERT dbo.Users(Login,FullName,PasswordHash,Role) OUTPUT INSERTED.Id VALUES(@login,@name,@hash,'User')",
            ("@login", login), ("@name", fullName), ("@hash", PasswordHasher.Hash(password)));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void ChangePassword(int id, string oldPassword, string password, string confirmation)
    {
        Validation.Password(password, confirmation);
        if (oldPassword == password) throw new DomainException("Новый пароль должен отличаться от текущего.");
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        using var read = Database.Command(connection, transaction,
            "SELECT PasswordHash FROM dbo.Users WITH(UPDLOCK) WHERE Id=@id", ("@id", id));
        var current = read.ExecuteScalar() as string;
        if (current == null || !PasswordHasher.Verify(oldPassword, current))
            throw new DomainException("Текущий пароль неверен.");
        using var write = Database.Command(connection, transaction,
            "UPDATE dbo.Users SET PasswordHash=@hash WHERE Id=@id", ("@hash", PasswordHasher.Hash(password)), ("@id", id));
        write.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<Account> Users(int actor)
    {
        using var connection = database.Open();
        Require(connection, null, actor, true);
        using var command = Database.Command(connection, null, "SELECT Id,Login,FullName,Role,Photo FROM dbo.Users ORDER BY FullName,Id");
        using var reader = command.ExecuteReader();
        var result = new List<Account>();
        while (reader.Read()) result.Add(ReadAccount(reader));
        return result;
    }

    public int SaveUser(int actor, int id, string login, string fullName, string role, string password, byte[]? photo)
    {
        login = Validation.Login(login);
        fullName = Validation.FullName(fullName);
        if (role is not ("Admin" or "User")) throw new DomainException("Выберите роль.");
        if (id == 0 || password.Length > 0) Validation.Password(password);
        if (photo?.Length > 2097152) throw new DomainException("Фотография не должна превышать 2 МБ.");
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        Require(connection, transaction, actor, true);
        if (id == actor && role != "Admin") throw new DomainException("Нельзя изменить собственную роль администратора.");
        if (id != 0)
        {
            var existing = Require(connection, transaction, id, false);
            if (existing.IsAdmin && role != "Admin") EnsureOtherAdmin(connection, transaction, id);
        }
        var hash = password.Length > 0 ? PasswordHasher.Hash(password) : null;
        var sql = id == 0
            ? "INSERT dbo.Users(Login,FullName,Role,PasswordHash,Photo) OUTPUT INSERTED.Id VALUES(@login,@name,@role,@hash,@photo)"
            : "UPDATE dbo.Users SET Login=@login,FullName=@name,Role=@role,PasswordHash=COALESCE(@hash,PasswordHash),Photo=@photo OUTPUT INSERTED.Id WHERE Id=@id";
        using var command = Database.Command(connection, transaction, sql, ("@id", id), ("@login", login), ("@name", fullName), ("@role", role));
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 200).Value = (object?)hash ?? DBNull.Value;
        command.Parameters.Add("@photo", SqlDbType.VarBinary, -1).Value = (object?)photo ?? DBNull.Value;
        var result = command.ExecuteScalar();
        if (result == null) throw new DomainException("Учетная запись уже удалена.");
        transaction.Commit();
        return Convert.ToInt32(result);
    }

    private static void EnsureOtherAdmin(SqlConnection connection, SqlTransaction transaction, int id)
    {
        using var command = Database.Command(connection, transaction,
            "SELECT COUNT(*) FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE Role='Admin' AND Id<>@id", ("@id", id));
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) throw new DomainException("Нельзя удалить или понизить последнего администратора.");
    }

    public List<Garden> Gardens(int actor)
    {
        using var connection = database.Open();
        Require(connection, null, actor, false);
        using var command = Database.Command(connection, null,
            "SELECT k.Id,k.Name,k.Address,k.Phone,k.Capacity,(SELECT COUNT(*) FROM dbo.Children c WHERE c.KindergartenId=k.Id AND c.Status=N'Зачислен') FROM dbo.Kindergartens k ORDER BY k.Name,k.Id");
        using var reader = command.ExecuteReader();
        var result = new List<Garden>();
        while (reader.Read()) result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5)));
        return result;
    }

    public int SaveGarden(int actor, int id, string name, string address, string phone, int capacity)
    {
        name = Validation.Required(name, "Название", 100);
        address = Validation.Required(address, "Адрес", 200);
        phone = Validation.Phone(phone);
        if (capacity is < 1 or > 500) throw new DomainException("Вместимость: от 1 до 500 мест.");
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        Require(connection, transaction, actor, true);
        if (id != 0)
        {
            using var count = Database.Command(connection, transaction,
                "SELECT COUNT(*) FROM dbo.Children WHERE KindergartenId=@id AND Status=N'Зачислен'", ("@id", id));
            if (Convert.ToInt32(count.ExecuteScalar()) > capacity)
                throw new DomainException("Вместимость меньше числа зачисленных воспитанников.");
        }
        var sql = id == 0
            ? "INSERT dbo.Kindergartens(Name,Address,Phone,Capacity) OUTPUT INSERTED.Id VALUES(@name,@address,@phone,@capacity)"
            : "UPDATE dbo.Kindergartens SET Name=@name,Address=@address,Phone=@phone,Capacity=@capacity OUTPUT INSERTED.Id WHERE Id=@id";
        using var command = Database.Command(connection, transaction, sql,
            ("@id", id), ("@name", name), ("@address", address), ("@phone", phone), ("@capacity", capacity));
        var result = command.ExecuteScalar();
        if (result == null) throw new DomainException("Детский сад уже удален.");
        transaction.Commit();
        return Convert.ToInt32(result);
    }

    public List<Child> Children(int actor)
    {
        using var connection = database.Open();
        var account = Require(connection, null, actor, false);
        using var command = Database.Command(connection, null,
            "SELECT c.Id,c.KindergartenId,c.ParentId,c.FullName,c.BirthDate,c.EnrollmentDate,c.GroupName,c.Status,c.MonthlyFee,k.Name,u.FullName FROM dbo.Children c JOIN dbo.Kindergartens k ON k.Id=c.KindergartenId JOIN dbo.Users u ON u.Id=c.ParentId WHERE @admin=1 OR c.ParentId=@actor ORDER BY c.FullName,c.Id",
            ("@admin", account.IsAdmin), ("@actor", actor));
        using var reader = command.ExecuteReader();
        var result = new List<Child>();
        while (reader.Read()) result.Add(new(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3),reader.GetDateTime(4),reader.GetDateTime(5),reader.GetString(6),reader.GetString(7),reader.GetDecimal(8),reader.GetString(9),reader.GetString(10)));
        return result;
    }

    public int SaveChild(int actor, int id, int gardenId, int parentId, string fullName, DateTime birth,
        DateTime enrollment, string group, string status, decimal fee)
    {
        Validation.Child(fullName, birth, enrollment, group, status, fee);
        fullName = Validation.FullName(fullName);
        group = group.Trim();
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        Require(connection, transaction, actor, true);
        Require(connection, transaction, parentId, false);
        using var garden = Database.Command(connection, transaction,
            "SELECT Capacity FROM dbo.Kindergartens WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", ("@id", gardenId));
        var capacity = garden.ExecuteScalar();
        if (capacity == null) throw new DomainException("Выберите существующий детский сад.");
        using var count = Database.Command(connection, transaction,
            "SELECT COUNT(*) FROM dbo.Children WITH(UPDLOCK,HOLDLOCK) WHERE KindergartenId=@garden AND Status=N'Зачислен' AND Id<>@id",
            ("@garden", gardenId), ("@id", id));
        if (status == "Зачислен" && Convert.ToInt32(count.ExecuteScalar()) >= Convert.ToInt32(capacity))
            throw new DomainException("В выбранном детском саду нет свободных мест.");
        var sql = id == 0
            ? "INSERT dbo.Children(KindergartenId,ParentId,FullName,BirthDate,EnrollmentDate,GroupName,Status,MonthlyFee) OUTPUT INSERTED.Id VALUES(@garden,@parent,@name,@birth,@date,@group,@status,@fee)"
            : "UPDATE dbo.Children SET KindergartenId=@garden,ParentId=@parent,FullName=@name,BirthDate=@birth,EnrollmentDate=@date,GroupName=@group,Status=@status,MonthlyFee=@fee OUTPUT INSERTED.Id WHERE Id=@id";
        using var command = Database.Command(connection, transaction, sql, ("@id",id),("@garden",gardenId),("@parent",parentId),
            ("@name",fullName),("@birth",birth.Date),("@date",enrollment.Date),("@group",group),("@status",status),("@fee",fee));
        var result = command.ExecuteScalar();
        if (result == null) throw new DomainException("Воспитанник уже удален.");
        transaction.Commit();
        return Convert.ToInt32(result);
    }

    public int DependentChildren(int actor, string entity, int id)
    {
        using var connection = database.Open();
        Require(connection, null, actor, true);
        var column = entity switch { "Users" => "ParentId", "Kindergartens" => "KindergartenId", _ => throw new DomainException("Неизвестная таблица.") };
        using var command = Database.Command(connection, null, $"SELECT COUNT(*) FROM dbo.Children WHERE {column}=@id", ("@id",id));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Delete(int actor, string entity, int id)
    {
        if (entity is not ("Users" or "Kindergartens" or "Children")) throw new DomainException("Неизвестная таблица.");
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        Require(connection, transaction, actor, true);
        if (entity == "Users")
        {
            if (actor == id) throw new DomainException("Нельзя удалить собственную учетную запись.");
            if (Require(connection,transaction,id,false).IsAdmin) EnsureOtherAdmin(connection,transaction,id);
        }
        using var command = Database.Command(connection,transaction,$"DELETE FROM dbo.{entity} WHERE Id=@id",("@id",id));
        if (command.ExecuteNonQuery() == 0) throw new DomainException("Запись уже удалена.");
        transaction.Commit();
    }
}

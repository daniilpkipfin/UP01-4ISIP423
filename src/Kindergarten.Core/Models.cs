namespace Kindergarten.Core;

public sealed record Account(int Id, string Login, string FullName, string Role, byte[]? Photo = null)
{
    public bool IsAdmin => Role == "Admin";
    public string RoleName => IsAdmin ? "Администратор" : "Пользователь";
}

public sealed record Garden(int Id, string Name, string Address, string Phone, int Capacity, int Enrolled)
{
    public int Available => Capacity - Enrolled;
}

public sealed record Child(int Id, int GardenId, int ParentId, string FullName, DateTime BirthDate,
    DateTime EnrollmentDate, string GroupName, string Status, decimal MonthlyFee,
    string GardenName, string ParentName)
{
    public string BirthText => BirthDate.ToString("dd.MM.yyyy");
    public string EnrollmentText => EnrollmentDate.ToString("dd.MM.yyyy");
}

public sealed record ReportData(string Title, string[] Headers, IReadOnlyList<string[]> Rows, string Filter);

public sealed class DomainException(string message) : Exception(message);

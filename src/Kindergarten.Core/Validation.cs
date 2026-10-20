using System.Text.RegularExpressions;

namespace Kindergarten.Core;

public static partial class Validation
{
    public static string Required(string value, string label, int max)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > max)
            throw new DomainException($"{label}: введите от 1 до {max} символов.");
        return value;
    }

    public static string Login(string value)
    {
        value = value.Trim();
        if (!LoginPattern().IsMatch(value))
            throw new DomainException("Логин: от 3 до 50 латинских букв, цифр, точек, дефисов или знаков подчеркивания.");
        return value;
    }

    public static string FullName(string value)
    {
        value = Regex.Replace(value.Trim(), @"\s+", " ");
        if (value.Length > 150 || !NamePattern().IsMatch(value))
            throw new DomainException("ФИО: укажите фамилию и имя буквами. Отчество при наличии. Максимум 150 символов.");
        return value;
    }

    public static void Password(string value, string? confirmation = null)
    {
        if (value.Length is < 6 or > 128 || !PasswordPattern().IsMatch(value) || !value.Any(char.IsDigit))
            throw new DomainException("Пароль: 6-128 символов, только латинские буквы и цифры, минимум одна цифра.");
        if (confirmation != null && value != confirmation)
            throw new DomainException("Пароли не совпадают.");
    }

    public static string Phone(string value)
    {
        value = value.Trim();
        if (!Regex.IsMatch(value, @"^\+7\d{10}$"))
            throw new DomainException("Телефон: +7 и десять цифр, например +74951234567.");
        return value;
    }

    public static void Child(string name, DateTime birth, DateTime enrollment, string group, string status, decimal fee)
    {
        FullName(name);
        Required(group, "Группа", 50);
        if (birth.Date < DateTime.Today.AddYears(-10) || birth.Date > DateTime.Today)
            throw new DomainException("Возраст воспитанника должен быть от 0 до 10 лет.");
        if (enrollment.Date < birth.Date || enrollment.Date > DateTime.Today)
            throw new DomainException("Дата зачисления должна быть не раньше рождения и не позже текущей даты.");
        if (!Statuses.Contains(status)) throw new DomainException("Выберите статус воспитанника.");
        if (fee is < 0 or > 100000 || decimal.Round(fee, 2) != fee)
            throw new DomainException("Плата: от 0 до 100000 рублей, не более двух знаков после запятой.");
    }

    public static readonly string[] Statuses = ["Зачислен", "Ожидает", "Выпущен"];

    [GeneratedRegex(@"^[A-Za-z0-9_.-]{3,50}$")]
    private static partial Regex LoginPattern();
    [GeneratedRegex(@"^\p{L}+(?:[- ]\p{L}+)+$")]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"^[A-Za-z0-9]+$")]
    private static partial Regex PasswordPattern();
}

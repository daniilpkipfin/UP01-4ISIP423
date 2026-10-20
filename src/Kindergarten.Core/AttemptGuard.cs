using System.Security.Cryptography;

namespace Kindergarten.Core;

public sealed class AttemptGuard
{
    private readonly string path;
    private string challenge = "";
    public int Failures { get; private set; }
    public bool CaptchaRequired => Failures >= 3;
    public string Challenge => challenge;

    public AttemptGuard(string file)
    {
        path = file;
        if (File.Exists(path))
            Failures = int.TryParse(File.ReadAllText(path), out var count) ? Math.Clamp(count, 0, 1000) : 3;
        Refresh();
    }

    public string Refresh()
    {
        const string symbols = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        challenge = new string(Enumerable.Range(0, 6).Select(_ => symbols[RandomNumberGenerator.GetInt32(symbols.Length)]).ToArray());
        return challenge;
    }

    public bool Check(string answer) => !CaptchaRequired || string.Equals(answer.Trim(), challenge, StringComparison.OrdinalIgnoreCase);

    public void Fail()
    {
        Failures = Math.Min(1000, Failures + 1);
        Save();
        Refresh();
    }

    public void Success()
    {
        Failures = 0;
        Save();
        Refresh();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Failures.ToString());
    }
}

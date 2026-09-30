using System.Security.Cryptography;

namespace APCVehicleTracker.API.Services;

public static class TemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%*?";

    public static string Generate(int length = 16)
    {
        if (length < 8) throw new ArgumentOutOfRangeException(nameof(length));
        var all = Upper + Lower + Digits + Symbols;

        var chars = new List<char>
        {
            Upper[RandomNumberGenerator.GetInt32(Upper.Length)],
            Lower[RandomNumberGenerator.GetInt32(Lower.Length)],
            Digits[RandomNumberGenerator.GetInt32(Digits.Length)],
            Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]
        };

        while (chars.Count < length)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        return new string(chars.ToArray());
    }
}

using System.Security.Cryptography;
using Adhicom.Application.Common;

namespace Adhicom.Infrastructure.Services;

public sealed class Clock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sans ambiguïté (0/O, 1/I)

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(5);
        var chars = new char[5];
        for (var i = 0; i < 5; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return $"AHC-{DateTime.UtcNow:yyyyMMdd}-{new string(chars)}";
    }
}

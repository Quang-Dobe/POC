namespace Poc.Bff.Infrastructure.Invites;

using System.Security.Cryptography;
using Poc.Bff.Application.Abstractions;

/// <summary>
/// Generates a cryptographically-random default password that satisfies both Keycloak (DEV)
/// and Azure Entra default complexity: at least one lower, upper, digit and symbol.
/// </summary>
public sealed class CryptoPasswordGenerator : IPasswordGenerator
{
    private const int Length = 16;

    // Ambiguous glyphs (0/O, 1/l/I) are intentionally excluded so a manager can read the
    // generated password aloud without confusion.
    private const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*-_";
    private const string All = Lowercase + Uppercase + Digits + Symbols;

    public string Generate()
    {
        var chars = new char[Length];

        // Guarantee one character from each class up front, then fill the remainder freely.
        chars[0] = Pick(Lowercase);
        chars[1] = Pick(Uppercase);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < Length; i++)
        {
            chars[i] = Pick(All);
        }

        Shuffle(chars);
        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

    private static void Shuffle(char[] chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}

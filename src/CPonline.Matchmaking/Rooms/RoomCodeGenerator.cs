using System.Security.Cryptography;

namespace CPonline.Matchmaking.Rooms;

/// <summary>Generates short room codes for players to read aloud/type over voice chat.
/// Excludes visually ambiguous characters (0/O, 1/I/L).</summary>
public static class RoomCodeGenerator
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    public static string Generate() => Generate(RandomNumberGenerator.GetInt32);

    /// <summary>Exposed for tests: <paramref name="nextInt"/> maps an exclusive upper bound to a
    /// random index in [0, bound), so a deterministic stub yields a predictable code.</summary>
    internal static string Generate(Func<int, int> nextInt)
    {
        var buffer = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[nextInt(Alphabet.Length)];
        }

        return new string(buffer);
    }
}

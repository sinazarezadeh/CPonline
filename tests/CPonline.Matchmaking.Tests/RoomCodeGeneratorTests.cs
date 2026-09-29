using CPonline.Matchmaking.Rooms;
using Xunit;

namespace CPonline.Matchmaking.Tests;

public class RoomCodeGeneratorTests
{
    [Fact]
    public void Generate_ProducesTheConfiguredLength()
    {
        var code = RoomCodeGenerator.Generate();

        Assert.Equal(RoomCodeGenerator.Length, code.Length);
    }

    [Fact]
    public void Generate_OnlyUsesCharactersFromTheAlphabet()
    {
        var code = RoomCodeGenerator.Generate();

        Assert.All(code, c => Assert.Contains(c, RoomCodeGenerator.Alphabet));
    }

    [Fact]
    public void Generate_ExcludesVisuallyAmbiguousCharacters()
    {
        Assert.DoesNotContain('0', RoomCodeGenerator.Alphabet);
        Assert.DoesNotContain('O', RoomCodeGenerator.Alphabet);
        Assert.DoesNotContain('1', RoomCodeGenerator.Alphabet);
        Assert.DoesNotContain('I', RoomCodeGenerator.Alphabet);
        Assert.DoesNotContain('L', RoomCodeGenerator.Alphabet);
    }

    [Fact]
    public void Generate_WithStubbedRandom_IsDeterministic()
    {
        var code = RoomCodeGenerator.Generate(_ => 0);

        Assert.Equal(new string(RoomCodeGenerator.Alphabet[0], RoomCodeGenerator.Length), code);
    }

    [Fact]
    public void Generate_PassesTheAlphabetLengthAsTheExclusiveBound()
    {
        var seenBounds = new List<int>();
        RoomCodeGenerator.Generate(bound =>
        {
            seenBounds.Add(bound);
            return 0;
        });

        Assert.All(seenBounds, b => Assert.Equal(RoomCodeGenerator.Alphabet.Length, b));
    }
}

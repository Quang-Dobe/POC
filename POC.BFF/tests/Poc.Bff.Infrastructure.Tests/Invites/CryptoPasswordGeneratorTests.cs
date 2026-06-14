namespace Poc.Bff.Infrastructure.Tests.Invites;

using System.Linq;
using Poc.Bff.Infrastructure.Invites;
using Xunit;

public class CryptoPasswordGeneratorTests
{
    private readonly CryptoPasswordGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsSixteenCharacters()
    {
        Assert.Equal(16, _generator.Generate().Length);
    }

    [Fact]
    public void Generate_SatisfiesEveryComplexityClass()
    {
        var password = _generator.Generate();

        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Generate_ProducesDistinctValues()
    {
        var values = Enumerable.Range(0, 50).Select(_ => _generator.Generate()).ToHashSet();

        // 16 random chars from a ~56-glyph alphabet: collisions are astronomically unlikely.
        Assert.Equal(50, values.Count);
    }
}

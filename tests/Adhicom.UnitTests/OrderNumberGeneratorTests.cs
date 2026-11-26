using System.Text.RegularExpressions;
using Adhicom.Infrastructure.Services;

namespace Adhicom.UnitTests;

public class OrderNumberGeneratorTests
{
    [Fact]
    public void Generates_AHC_Format_With_Unambiguous_Alphabet()
    {
        var generator = new OrderNumberGenerator();
        for (var i = 0; i < 50; i++)
        {
            var number = generator.Generate();
            Assert.Matches(new Regex("^AHC-\\d{8}-[A-Z2-9]{5}$", RegexOptions.CultureInvariant), number);
            Assert.DoesNotContain('0', number);
            Assert.DoesNotContain('O', number);
            Assert.DoesNotContain('I', number);
            Assert.DoesNotContain('1', number);
        }
    }

    [Fact]
    public void Generates_Distinct_Numbers()
    {
        var generator = new OrderNumberGenerator();
        var set = new HashSet<string>();
        for (var i = 0; i < 200; i++) set.Add(generator.Generate());
        Assert.True(set.Count > 190, "Les numéros doivent être virtuellement uniques (5 caractères sur 32 symboles).");
    }
}

using Adhicom.Application.Common;

namespace Adhicom.UnitTests;

public class WhatsAppLinkTests
{
    [Fact]
    public void Build_Normalizes_Number_And_Encodes_Message()
    {
        var url = WhatsAppLink.Build("+243 81 234-56-78", "Bonjour Fashion House ! Commande AHC-20261126-4F8K2");
        Assert.StartsWith("https://wa.me/243812345678?text=", url);
        Assert.DoesNotContain("+", url.Split('?')[0].Replace("wa.me/", ""));
        Assert.Contains("AHC-20261126-4F8K2", Uri.UnescapeDataString(url));
    }

    [Fact]
    public void Build_With_Null_Number_Produces_Empty_Target()
    {
        var url = WhatsAppLink.Build(null, "test");
        Assert.StartsWith("https://wa.me/?text=", url);
    }

    [Fact]
    public void Special_Characters_Are_Escaped()
    {
        var url = WhatsAppLink.Build("+243810000001", "a&b=c d?e");
        Assert.Contains("a%26b%3Dc%20d%3Fe", url);
    }
}

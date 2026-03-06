using Anduril.Core.AI;

namespace Anduril.Core.Tests;

public class AiProviderOptionsTests
{
    [Test]
    public async Task DefaultValues_AreCorrect()
    {
        var options = new AiProviderOptions();

        await Assert.That(options.Enabled).IsTrue();
        await Assert.That(options.Provider).IsEqualTo(string.Empty);
        await Assert.That(options.Model).IsEqualTo(string.Empty);
        await Assert.That(options.ApiKey).IsNull();
        await Assert.That(options.Endpoint).IsNull();
        await Assert.That(options.ModelPath).IsNull();
        await Assert.That(options.AugmentCliPath).IsNull();
    }
}


using Anduril.AI.Detection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.AI.Tests;

public class OllamaDetectorTests
{
    [Test]
    public async Task Constructor_DoesNotThrow()
    {
        var detector = new OllamaDetector(NullLogger<OllamaDetector>.Instance);
        await Assert.That(detector).IsNotNull();
    }
}


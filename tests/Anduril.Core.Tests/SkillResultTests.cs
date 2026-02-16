using Anduril.Core.Skills;

namespace Anduril.Core.Tests;

public class SkillResultTests
{
    [Test]
    public async Task Ok_ReturnsSuccessResult()
    {
        var result = SkillResult.Ok("Hello!");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsEqualTo("Hello!");
        await Assert.That(result.ErrorMessage).IsNull();
        await Assert.That(result.Data).IsNull();
    }

    [Test]
    public async Task Ok_WithData_ReturnsSuccessResultWithData()
    {
        var data = new { Count = 42 };
        var result = SkillResult.Ok("Done", data);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsEqualTo("Done");
        await Assert.That(result.Data).IsSameReferenceAs(data);
    }

    [Test]
    public async Task Fail_ReturnsFailureResult()
    {
        var result = SkillResult.Fail("Something went wrong");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Something went wrong");
        await Assert.That(result.Response).IsEqualTo("Something went wrong");
    }
}


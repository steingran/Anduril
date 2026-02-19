using Anduril.AI.Providers;
using Anduril.Core.AI;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class AnthropicProviderTests
{
    [Test]
    public async Task Name_IsAnthropic()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "anthropic", Model = "claude-sonnet-4-20250514" });
        var provider = new AnthropicProvider(options, NullLogger<AnthropicProvider>.Instance);

        await Assert.That(provider.Name).IsEqualTo("anthropic");
    }

    [Test]
    public async Task MergeOptions_WhenPromptCachingEnabled_SetsRawRepresentationFactory()
    {
        var inner = new FakeChatClient();
        var wrapper = new AnthropicChatClientWrapper(inner, "claude-sonnet-4-5", enablePromptCaching: true);

        // Trigger a call so MergeOptions runs — the fake captures the options
        await wrapper.GetResponseAsync([new ChatMessage(ChatRole.User, "test")]);

        var capturedOptions = inner.LastOptions;
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.RawRepresentationFactory).IsNotNull();

        // Invoke the factory and verify it returns MessageParameters with caching enabled
        var result = capturedOptions.RawRepresentationFactory!(inner);
        await Assert.That(result).IsTypeOf<MessageParameters>();

        var parameters = (MessageParameters)result!;
        await Assert.That(parameters.PromptCaching).IsEqualTo(PromptCacheType.AutomaticToolsAndSystem);
    }

    [Test]
    public async Task MergeOptions_WhenPromptCachingDisabled_DoesNotSetRawRepresentationFactory()
    {
        var inner = new FakeChatClient();
        var wrapper = new AnthropicChatClientWrapper(inner, "claude-sonnet-4-5", enablePromptCaching: false);

        await wrapper.GetResponseAsync([new ChatMessage(ChatRole.User, "test")]);

        var capturedOptions = inner.LastOptions;
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.RawRepresentationFactory).IsNull();
    }

    [Test]
    public async Task MergeOptions_WhenPromptCachingEnabled_PreservesExistingRawRepresentationFactory()
    {
        var inner = new FakeChatClient();
        var wrapper = new AnthropicChatClientWrapper(inner, "claude-sonnet-4-5", enablePromptCaching: true);

        // Pass options with a pre-existing RawRepresentationFactory that sets MaxTokens
        var existingOptions = new ChatOptions
        {
            RawRepresentationFactory = _ =>
            {
                var p = new MessageParameters { MaxTokens = 9999 };
                return p;
            }
        };

        await wrapper.GetResponseAsync([new ChatMessage(ChatRole.User, "test")], existingOptions);

        var capturedOptions = inner.LastOptions;
        var result = capturedOptions!.RawRepresentationFactory!(inner);
        var parameters = (MessageParameters)result!;

        // Both the existing factory's MaxTokens and our caching should be preserved
        await Assert.That(parameters.MaxTokens).IsEqualTo(9999);
        await Assert.That(parameters.PromptCaching).IsEqualTo(PromptCacheType.AutomaticToolsAndSystem);
    }

    [Test]
    public async Task MergeOptions_SetsDefaultModel_WhenNotSpecified()
    {
        var inner = new FakeChatClient();
        var wrapper = new AnthropicChatClientWrapper(inner, "claude-sonnet-4-5");

        await wrapper.GetResponseAsync([new ChatMessage(ChatRole.User, "test")]);

        await Assert.That(inner.LastOptions!.ModelId).IsEqualTo("claude-sonnet-4-5");
    }

    [Test]
    public async Task MergeOptions_DoesNotOverrideModel_WhenSpecifiedByCaller()
    {
        var inner = new FakeChatClient();
        var wrapper = new AnthropicChatClientWrapper(inner, "claude-sonnet-4-5");

        var callerOptions = new ChatOptions { ModelId = "claude-opus-4" };
        await wrapper.GetResponseAsync([new ChatMessage(ChatRole.User, "test")], callerOptions);

        await Assert.That(inner.LastOptions!.ModelId).IsEqualTo("claude-opus-4");
    }
}


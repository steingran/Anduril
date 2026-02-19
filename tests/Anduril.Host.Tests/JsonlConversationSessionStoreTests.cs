using System.Text;
using System.Text.Json;
using Anduril.Core.Communication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Tests;

public class JsonlConversationSessionStoreTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "anduril_tests_" + Guid.NewGuid().ToString("N"));
    }

    [After(Test)]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---------------------------------------------------------------
    // Load — empty / non-existent
    // ---------------------------------------------------------------

    [Test]
    public async Task LoadAsync_NoSessionFile_ReturnsEmptyList()
    {
        var store = CreateStore();

        var messages = await store.LoadAsync("slack:U12345");

        await Assert.That(messages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Append + Load roundtrip
    // ---------------------------------------------------------------

    [Test]
    public async Task AppendAsync_ThenLoadAsync_ReturnsSameMessage()
    {
        var store = CreateStore();
        var timestamp = DateTimeOffset.UtcNow;
        var msg = new SessionMessage("user", "hello world", timestamp);

        await store.AppendAsync("slack:U999", msg);
        var loaded = await store.LoadAsync("slack:U999");

        await Assert.That(loaded.Count).IsEqualTo(1);
        await Assert.That(loaded[0].Role).IsEqualTo("user");
        await Assert.That(loaded[0].Content).IsEqualTo("hello world");
        await Assert.That(loaded[0].Timestamp).IsEqualTo(timestamp);
    }

    [Test]
    public async Task AppendAsync_MultipleMessages_PreservesOrder()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;

        await store.AppendAsync("cli:stein", new SessionMessage("user", "first", now));
        await store.AppendAsync("cli:stein", new SessionMessage("assistant", "second", now.AddSeconds(1)));
        await store.AppendAsync("cli:stein", new SessionMessage("user", "third", now.AddSeconds(2)));

        var loaded = await store.LoadAsync("cli:stein");

        await Assert.That(loaded.Count).IsEqualTo(3);
        await Assert.That(loaded[0].Content).IsEqualTo("first");
        await Assert.That(loaded[1].Content).IsEqualTo("second");
        await Assert.That(loaded[2].Content).IsEqualTo("third");
    }

    // ---------------------------------------------------------------
    // JSONL file format
    // ---------------------------------------------------------------

    [Test]
    public async Task AppendAsync_WritesValidJsonlFormat()
    {
        var store = CreateStore();
        var now = new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero);

        await store.AppendAsync("test:format", new SessionMessage("user", "hi", now));
        await store.AppendAsync("test:format", new SessionMessage("assistant", "hello", now.AddSeconds(1)));

        string filePath = Directory.GetFiles(_tempDir, "*.jsonl").Single();
        string[] lines = (await File.ReadAllTextAsync(filePath))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines.Length).IsEqualTo(2);

        // Each line should be valid JSON with camelCase properties
        var first = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        await Assert.That(first.GetProperty("role").GetString()).IsEqualTo("user");
        await Assert.That(first.GetProperty("content").GetString()).IsEqualTo("hi");
        await Assert.That(first.TryGetProperty("timestamp", out _)).IsTrue();
    }

    // ---------------------------------------------------------------
    // Session key sanitization
    // ---------------------------------------------------------------

    [Test]
    public async Task SessionKey_WithColons_ProducesValidFilename()
    {
        var store = CreateStore();
        await store.AppendAsync("slack:U12345", new SessionMessage("user", "test", DateTimeOffset.UtcNow));

        string[] files = Directory.GetFiles(_tempDir, "*.jsonl");
        await Assert.That(files.Length).IsEqualTo(1);

        // The file should exist and be readable
        var loaded = await store.LoadAsync("slack:U12345");
        await Assert.That(loaded.Count).IsEqualTo(1);
    }

    // ---------------------------------------------------------------
    // Separate sessions per key
    // ---------------------------------------------------------------

    [Test]
    public async Task DifferentSessionKeys_MaintainSeparateHistories()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;

        await store.AppendAsync("slack:user-a", new SessionMessage("user", "from A", now));
        await store.AppendAsync("slack:user-b", new SessionMessage("user", "from B", now));

        var loadedA = await store.LoadAsync("slack:user-a");
        var loadedB = await store.LoadAsync("slack:user-b");

        await Assert.That(loadedA.Count).IsEqualTo(1);
        await Assert.That(loadedA[0].Content).IsEqualTo("from A");
        await Assert.That(loadedB.Count).IsEqualTo(1);
        await Assert.That(loadedB[0].Content).IsEqualTo("from B");
    }

    // ---------------------------------------------------------------
    // ReplaceAllAsync
    // ---------------------------------------------------------------

    [Test]
    public async Task ReplaceAllAsync_OverwritesExistingMessages()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;

        // Write 5 messages
        for (int i = 1; i <= 5; i++)
            await store.AppendAsync("replace:test", new SessionMessage("user", $"msg-{i}", now.AddSeconds(i)));

        // Replace with 2 compacted messages
        var compacted = new List<SessionMessage>
        {
            new("system", "[Previous conversation summary]\nUser discussed topics 1-3.", now),
            new("user", "msg-4", now.AddSeconds(4)),
            new("assistant", "reply-5", now.AddSeconds(5))
        };
        await store.ReplaceAllAsync("replace:test", compacted);

        var loaded = await store.LoadAsync("replace:test");

        await Assert.That(loaded.Count).IsEqualTo(3);
        await Assert.That(loaded[0].Content).StartsWith("[Previous conversation summary]");
        await Assert.That(loaded[1].Content).IsEqualTo("msg-4");
        await Assert.That(loaded[2].Content).IsEqualTo("reply-5");
    }

    [Test]
    public async Task ReplaceAllAsync_RewritesFileOnDisk()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;

        for (int i = 1; i <= 5; i++)
            await store.AppendAsync("replace:disk", new SessionMessage("user", $"msg-{i}", now.AddSeconds(i)));

        var compacted = new List<SessionMessage>
        {
            new("user", "summary", now),
            new("user", "msg-5", now.AddSeconds(5))
        };
        await store.ReplaceAllAsync("replace:disk", compacted);

        // Read the raw file — should have exactly 2 lines
        string filePath = Directory.GetFiles(_tempDir, "*.jsonl").Single();
        string[] lines = (await File.ReadAllTextAsync(filePath))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines.Length).IsEqualTo(2);
    }

    [Test]
    public async Task LoadAsync_ReturnsAllMessages_NoTrimming()
    {
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;

        // Append many messages — store should return all of them (no trimming)
        for (int i = 1; i <= 100; i++)
            await store.AppendAsync("notrim:test", new SessionMessage("user", $"msg-{i}", now.AddSeconds(i)));

        var loaded = await store.LoadAsync("notrim:test");

        await Assert.That(loaded.Count).IsEqualTo(100);
        await Assert.That(loaded[0].Content).IsEqualTo("msg-1");
        await Assert.That(loaded[99].Content).IsEqualTo("msg-100");
    }

    // ---------------------------------------------------------------
    // Malformed line handling
    // ---------------------------------------------------------------

    [Test]
    public async Task LoadAsync_SkipsMalformedLines_ReturnsValidOnes()
    {
        var store = CreateStore();

        // Write a file with a mix of valid and invalid lines
        string filePath = GetSessionFilePath("malformed_test");
        var validMsg = new SessionMessage("user", "good line", DateTimeOffset.UtcNow);
        string validJson = JsonSerializer.Serialize(validMsg, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, $"{validJson}\nNOT_VALID_JSON\n{validJson}\n");

        var loaded = await store.LoadAsync("malformed_test");

        await Assert.That(loaded.Count).IsEqualTo(2);
        await Assert.That(loaded[0].Content).IsEqualTo("good line");
        await Assert.That(loaded[1].Content).IsEqualTo("good line");
    }

    [Test]
    public async Task LoadAsync_SkipsBlankLines()
    {
        var store = CreateStore();
        string filePath = GetSessionFilePath("blank_lines");
        var msg = new SessionMessage("user", "data", DateTimeOffset.UtcNow);
        string json = JsonSerializer.Serialize(msg, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, $"\n{json}\n\n{json}\n\n");

        var loaded = await store.LoadAsync("blank_lines");

        await Assert.That(loaded.Count).IsEqualTo(2);
    }

    // ---------------------------------------------------------------
    // Persistence across store instances
    // ---------------------------------------------------------------

    [Test]
    public async Task Messages_SurviveStoreRecreation()
    {
        var store1 = CreateStore();
        var now = DateTimeOffset.UtcNow;
        await store1.AppendAsync("persist:test", new SessionMessage("user", "my name is Stein", now));
        await store1.AppendAsync("persist:test", new SessionMessage("assistant", "Nice to meet you, Stein!", now.AddSeconds(1)));

        // Create a brand new store instance pointing at the same directory
        var store2 = CreateStore();
        var loaded = await store2.LoadAsync("persist:test");

        await Assert.That(loaded.Count).IsEqualTo(2);
        await Assert.That(loaded[0].Content).IsEqualTo("my name is Stein");
        await Assert.That(loaded[1].Content).IsEqualTo("Nice to meet you, Stein!");
    }

    // ---------------------------------------------------------------
    // Constructor creates directory
    // ---------------------------------------------------------------

    [Test]
    public async Task Constructor_CreatesSessionsDirectory()
    {
        string nestedDir = Path.Combine(_tempDir, "nested", "sessions");
        _ = CreateStore(sessionsDirectory: nestedDir);

        await Assert.That(Directory.Exists(nestedDir)).IsTrue();
    }

    // ---------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------

    private JsonlConversationSessionStore CreateStore(string? sessionsDirectory = null)
    {
        var options = Options.Create(new ConversationSessionOptions
        {
            SessionsDirectory = sessionsDirectory ?? _tempDir
        });
        return new JsonlConversationSessionStore(options, NullLogger<JsonlConversationSessionStore>.Instance);
    }

    /// <summary>
    /// Computes the hex-encoded file path for a session key, matching the store's internal logic.
    /// </summary>
    private string GetSessionFilePath(string sessionKey)
    {
        string hexName = Convert.ToHexString(Encoding.UTF8.GetBytes(sessionKey));
        return Path.Combine(_tempDir, $"{hexName}.jsonl");
    }
}

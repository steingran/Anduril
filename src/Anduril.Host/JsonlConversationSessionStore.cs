using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Anduril.Core.Communication;
using Microsoft.Extensions.Options;

namespace Anduril.Host;

/// <summary>
/// Persists conversation sessions as JSONL files on disk.
/// Each session is a separate file named <c>{sessionKey}.jsonl</c> where each line
/// is a JSON-serialized <see cref="SessionMessage"/>.
/// Append-only writes ensure crash safety — at most one line is lost on a crash.
/// </summary>
public sealed class JsonlConversationSessionStore : IConversationSessionStore
{
    private readonly ConversationSessionOptions _options;
    private readonly ILogger<JsonlConversationSessionStore> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JsonlConversationSessionStore(
        IOptions<ConversationSessionOptions> options,
        ILogger<JsonlConversationSessionStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        Directory.CreateDirectory(_options.SessionsDirectory);
    }

    public async Task<IReadOnlyList<SessionMessage>> LoadAsync(
        string sessionKey, CancellationToken cancellationToken = default)
    {
        var semaphore = GetSessionLock(sessionKey);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            string path = GetSessionPath(sessionKey);
            if (!File.Exists(path))
                return [];

            var messages = new List<SessionMessage>();
            try
            {
                await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var msg = JsonSerializer.Deserialize<SessionMessage>(line, JsonOptions);
                        if (msg is not null)
                            messages.Add(msg);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Skipping malformed line in session '{SessionKey}'", sessionKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session '{SessionKey}'", sessionKey);
                return [];
            }

            return messages;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task AppendAsync(
        string sessionKey, SessionMessage message, CancellationToken cancellationToken = default)
    {
        var semaphore = GetSessionLock(sessionKey);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            string path = GetSessionPath(sessionKey);

            try
            {
                string line = JsonSerializer.Serialize(message, JsonOptions) + Environment.NewLine;
                await File.AppendAllTextAsync(path, line, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to append to session '{SessionKey}'", sessionKey);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ReplaceAllAsync(
        string sessionKey, IReadOnlyList<SessionMessage> messages, CancellationToken cancellationToken = default)
    {
        var semaphore = GetSessionLock(sessionKey);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            string path = GetSessionPath(sessionKey);

            try
            {
                await WriteAllAsync(path, messages, cancellationToken);
                _logger.LogDebug(
                    "Replaced session '{SessionKey}' with {Count} message(s)",
                    sessionKey, messages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replace session '{SessionKey}'", sessionKey);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private SemaphoreSlim GetSessionLock(string sessionKey) =>
        _sessionLocks.GetOrAdd(sessionKey, _ => new SemaphoreSlim(1, 1));

    private string GetSessionPath(string sessionKey)
    {
        // Hex-encode the session key for collision-resistant, filesystem-safe filenames
        string hexName = Convert.ToHexString(Encoding.UTF8.GetBytes(sessionKey));
        return Path.Combine(_options.SessionsDirectory, $"{hexName}.jsonl");
    }

    private static async Task WriteAllAsync(
        string path, IReadOnlyList<SessionMessage> messages, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path, append: false);
        foreach (var msg in messages)
        {
            string line = JsonSerializer.Serialize(msg, JsonOptions);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}


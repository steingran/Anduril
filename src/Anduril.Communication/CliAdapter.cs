using Anduril.Core.Communication;
using Microsoft.Extensions.Logging;

namespace Anduril.Communication;

/// <summary>
/// Communication adapter for the local command-line interface.
/// Reads user input from stdin and writes responses to stdout.
/// Useful for development, testing, and headless/server operation.
/// </summary>
public sealed class CliAdapter(ILogger<CliAdapter> logger) : ICommunicationAdapter
{
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public string Platform => "cli";

    public bool IsConnected { get; private set; }

    public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsConnected = true;
        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        logger.LogInformation("CLI adapter started. Type a message and press Enter.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        logger.LogInformation("CLI adapter stopped.");
    }

    public Task<string?> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[Anduril] ");
        Console.ResetColor();
        Console.WriteLine(message.Text);
        Console.WriteLine();
        return Task.FromResult<string?>(null);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        int messageCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[You] ");
            Console.ResetColor();

            // Note: Console.ReadLine is a blocking call that does not respond to cancellation tokens.
            // To stop the CLI adapter cleanly, either:
            // 1. Type 'exit' or 'quit' and press Enter
            // 2. Press Ctrl+C to terminate the application
            // 3. Close stdin (e.g., EOF via Ctrl+D on Unix or Ctrl+Z on Windows)
            string? line = await Task.Run(Console.ReadLine, cancellationToken);

            if (line is null)
            {
                // EOF — stdin closed
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("User requested exit via CLI.");
                break;
            }

            messageCount++;

            var incoming = new IncomingMessage
            {
                Id = $"cli-{messageCount}",
                Text = line,
                UserId = Environment.UserName,
                UserName = Environment.UserName,
                ChannelId = "cli",
                Platform = Platform,
                IsDirectMessage = true
            };

            await MessageReceived.Invoke(incoming);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}


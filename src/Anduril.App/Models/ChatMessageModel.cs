using System.Windows.Input;
using ReactiveUI;

namespace Anduril.App.Models;

/// <summary>
/// UI-layer model representing a single chat message displayed in the conversation view.
/// </summary>
public sealed class ChatMessageModel : ReactiveObject
{
    private const int CollapseThreshold = 500;
    private const double CollapsedMaxHeight = 320.0;

    private bool _isExpanded;
    private bool _isLatestAssistant;
    private bool _isStreamingMessage;
    private string _content = string.Empty;
    private IReadOnlyList<ToolCallSummary> _toolCalls = [];
    private IReadOnlyList<ChatContentBlock> _contentBlocks = [];

    public ChatMessageModel()
    {
        ToggleExpandCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);
    }

    public required string Role { get; init; }

    public required string Content
    {
        get => _content;
        set
        {
            this.RaiseAndSetIfChanged(ref _content, value);
            ContentBlocks = ParseContentBlocks(value);
            this.RaisePropertyChanged(nameof(IsLoading));
            this.RaisePropertyChanged(nameof(IsCollapsible));
            this.RaisePropertyChanged(nameof(ContentMaxHeight));
            this.RaisePropertyChanged(nameof(EstimatedTokenCount));
            this.RaisePropertyChanged(nameof(TokenCountLabel));
        }
    }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ToolCallSummary> ToolCalls
    {
        get => _toolCalls;
        set
        {
            this.RaiseAndSetIfChanged(ref _toolCalls, value);
            this.RaisePropertyChanged(nameof(HasToolCalls));
        }
    }

    public IReadOnlyList<ChatContentBlock> ContentBlocks
    {
        get => _contentBlocks;
        private set => this.RaiseAndSetIfChanged(ref _contentBlocks, value);
    }

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsStopped => Role == "stopped";
    public bool IsLoading => IsAssistant && string.IsNullOrEmpty(Content);
    public bool HasToolCalls => ToolCalls.Count > 0;

    public bool IsCollapsible => Content.Length > CollapseThreshold;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(ContentMaxHeight));
            this.RaisePropertyChanged(nameof(ShowMoreLabel));
        }
    }

    public bool IsLatestAssistant
    {
        get => _isLatestAssistant;
        set => this.RaiseAndSetIfChanged(ref _isLatestAssistant, value);
    }

    public bool IsStreamingMessage
    {
        get => _isStreamingMessage;
        set => this.RaiseAndSetIfChanged(ref _isStreamingMessage, value);
    }

    public int EstimatedTokenCount => Math.Max(1, (int)Math.Ceiling(Content.Length / 4.0));
    public string TokenCountLabel => $"{EstimatedTokenCount} tok";
    public double ContentMaxHeight => IsCollapsible && !IsExpanded ? CollapsedMaxHeight : double.PositiveInfinity;
    public string ShowMoreLabel => IsExpanded ? "Show less" : "Show more";

    public ICommand ToggleExpandCommand { get; }

    private static IReadOnlyList<ChatContentBlock> ParseContentBlocks(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [new TextChatContentBlock(string.Empty)];

        var blocks = new List<ChatContentBlock>();
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var language = line[3..].Trim();
                var codeLines = new List<string>();
                index++;

                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(lines[index]);
                    index++;
                }

                if (index < lines.Length)
                    index++;

                blocks.Add(new CodeChatContentBlock(string.Join(Environment.NewLine, codeLines), string.IsNullOrWhiteSpace(language) ? null : language));
                continue;
            }

            if (LooksLikeTableRow(line))
            {
                var tableLines = new List<string>();

                while (index < lines.Length && LooksLikeTableRow(lines[index]))
                {
                    tableLines.Add(lines[index]);
                    index++;
                }

                if (TryBuildTableBlock(tableLines, out var tableBlock))
                {
                    blocks.Add(tableBlock);
                    continue;
                }

                blocks.Add(new TextChatContentBlock(string.Join(Environment.NewLine, tableLines)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            var paragraphLines = new List<string>();
            while (index < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[index]) &&
                   !lines[index].StartsWith("```", StringComparison.Ordinal) &&
                   !LooksLikeTableRow(lines[index]))
            {
                paragraphLines.Add(lines[index]);
                index++;
            }

            blocks.Add(new TextChatContentBlock(string.Join(Environment.NewLine, paragraphLines)));
        }

        return blocks.Count == 0 ? [new TextChatContentBlock(content)] : blocks;
    }

    private static bool LooksLikeTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.EndsWith('|') && trimmed.Count(ch => ch == '|') >= 2;
    }

    private static bool TryBuildTableBlock(IReadOnlyList<string> tableLines, out TableChatContentBlock block)
    {
        block = null!;

        if (tableLines.Count < 2)
            return false;

        var header = ParseTableCells(tableLines[0]);
        var separator = ParseTableCells(tableLines[1]);

        if (header.Count == 0 || separator.Count != header.Count || !separator.All(IsAlignmentCell))
            return false;

        var rows = new List<IReadOnlyList<string>>();
        for (var i = 2; i < tableLines.Count; i++)
        {
            var row = ParseTableCells(tableLines[i]);
            if (row.Count != header.Count)
                return false;

            rows.Add(row);
        }

        block = new TableChatContentBlock(header, rows);
        return true;
    }

    private static IReadOnlyList<string> ParseTableCells(string line) =>
        line.Trim()
            .Trim('|')
            .Split('|')
            .Select(cell => cell.Trim())
            .ToArray();

    private static bool IsAlignmentCell(string cell) =>
        cell.Length > 0 && cell.All(ch => ch is '-' or ':' or ' ');
}

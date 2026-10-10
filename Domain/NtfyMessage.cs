using System.Text.Json.Serialization;

namespace NtfyDesktop.Domain;

public sealed record NtfyMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;

    [JsonPropertyName("priority")]
    public Priority Priority { get; init; } = Priority.Default;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    // ntfy sets content_type to "text/markdown" when a message is published with the
    // Markdown header (or Content-Type: text/markdown). Absent/anything else → plain text.
    [JsonPropertyName("content_type")]
    public string? ContentType { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    [JsonPropertyName("click")]
    public string? Click { get; init; }

    [JsonPropertyName("attachment")]
    public NtfyAttachment? Attachment { get; init; }

    [JsonPropertyName("actions")]
    public List<NtfyAction>? Actions { get; init; }

    [JsonPropertyName("sequence_id")]
    public string? SequenceId { get; init; }

    [JsonPropertyName("expires")]
    public long? Expires { get; init; }

    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeSeconds(Time);

    /// <summary>
    /// Returns <see cref="SequenceId"/> if present (for messages updated via custom sequence ID / URL path),
    /// otherwise falls back to the server-generated <see cref="Id"/>.
    /// </summary>
    public string EffectiveId => !string.IsNullOrWhiteSpace(SequenceId) ? SequenceId : Id;

    /// <summary>ntfy flagged this body as Markdown (content_type == "text/markdown").</summary>
    public bool IsMarkdown =>
        string.Equals(ContentType, "text/markdown", StringComparison.OrdinalIgnoreCase);
}

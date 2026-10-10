using NtfyDesktop.Core.Messaging;

namespace NtfyDesktop.Features.History.Events;

public record MessageUpdated(HistoryMessage Message) : IEvent;

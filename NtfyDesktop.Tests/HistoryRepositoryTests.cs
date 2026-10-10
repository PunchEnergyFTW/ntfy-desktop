using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NtfyDesktop.Core.Messaging;
using NtfyDesktop.Domain;
using NtfyDesktop.Features.History;
using NtfyDesktop.Features.Settings;
using Xunit;

namespace NtfyDesktop.Tests;

public class HistoryRepositoryTests : IDisposable
{
    private readonly AppSettings _settings;
    private readonly HistoryRepository _repository;

    private class TestUIDispatcher : IUIDispatcher
    {
        public bool IsOnUIThread() => true;
        public async Task InvokeAsync(Func<Task> callback, CancellationToken ct = default) => await callback();
    }

    public HistoryRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IUIDispatcher>(new TestUIDispatcher());
        services.AddLogging();
        services.AddSingleton<EventBus>();
        var sp = services.BuildServiceProvider();
        EventBusHost.Initialize(sp.GetRequiredService<EventBus>());

        _settings = AppSettings.Load();
        _repository = new HistoryRepository(_settings);
        _repository.ClearAll();
    }

    public void Dispose()
    {
        var dbPath = Path.Combine(App.DataPath, "history.db");
        if (File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Insert_NewMessage_ReturnsInserted()
    {
        var msg = new NtfyMessage
        {
            Id = "msg_001",
            Topic = "test",
            Message = "Original Body",
            Title = "Original Title",
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var result = _repository.Insert(msg, topicId, serverId);

        Assert.Equal(MessageSaveResult.Inserted, result);

        var queried = _repository.Query(topicId: topicId);
        Assert.Single(queried);
        Assert.Equal("Original Body", queried[0].Body);
        Assert.Equal("Original Title", queried[0].Title);
    }

    [Fact]
    public void Insert_IdenticalMessage_ReturnsIgnored()
    {
        var msg = new NtfyMessage
        {
            Id = "msg_002",
            Topic = "test",
            Message = "Same Body",
            Title = "Same Title",
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var firstResult = _repository.Insert(msg, topicId, serverId);
        var secondResult = _repository.Insert(msg, topicId, serverId);

        Assert.Equal(MessageSaveResult.Inserted, firstResult);
        Assert.Equal(MessageSaveResult.Ignored, secondResult);

        var queried = _repository.Query(topicId: topicId);
        Assert.Single(queried);
    }

    [Fact]
    public void Insert_UpdatedMessage_UpdatesDatabaseAndReturnsUpdated()
    {
        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var msg1 = new NtfyMessage
        {
            Id = "msg_003",
            Topic = "test",
            Message = "Initial Body",
            Title = "Initial Title",
            Time = timestamp
        };

        var msg2 = new NtfyMessage
        {
            Id = "msg_003",
            Topic = "test",
            Message = "Updated Body text",
            Title = "Updated Title text",
            Time = timestamp + 5
        };

        var insertResult = _repository.Insert(msg1, topicId, serverId);
        var updateResult = _repository.Insert(msg2, topicId, serverId);

        Assert.Equal(MessageSaveResult.Inserted, insertResult);
        Assert.Equal(MessageSaveResult.Updated, updateResult);

        var queried = _repository.Query(topicId: topicId);
        Assert.Single(queried);
        Assert.Equal("Updated Body text", queried[0].Body);
        Assert.Equal("Updated Title text", queried[0].Title);
    }

    [Fact]
    public void Insert_AfterDelete_WithSameMessageId_NewTimestamp_ReturnsInserted()
    {
        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var msg1 = new NtfyMessage
        {
            Id = "msg_004",
            Topic = "test",
            Message = "Original Body",
            Time = timestamp
        };

        _repository.Insert(msg1, topicId, serverId);
        var stored = _repository.Query(topicId: topicId);
        Assert.Single(stored);

        // User deletes message from history
        _repository.DeleteByRowId(stored[0].RowId, MessageDeletionSource.Feed);
        Assert.Empty(_repository.Query(topicId: topicId));

        // Now a new/updated message arrives with same msg-id but newer timestamp
        var msg2 = new NtfyMessage
        {
            Id = "msg_004",
            Topic = "test",
            Message = "Re-sent Body",
            Time = timestamp + 10
        };

        var result = _repository.Insert(msg2, topicId, serverId);
        Assert.Equal(MessageSaveResult.Inserted, result);

        var queried = _repository.Query(topicId: topicId);
        Assert.Single(queried);
        Assert.Equal("Re-sent Body", queried[0].Body);
    }

    [Fact]
    public void Insert_AfterDelete_LiveUpdateWithSameMessageId_ReturnsInserted()
    {
        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var msg1 = new NtfyMessage
        {
            Id = "msg_005",
            Topic = "test",
            Message = "Original Body",
            Time = timestamp
        };

        _repository.Insert(msg1, topicId, serverId);
        var stored = _repository.Query(topicId: topicId);
        Assert.Single(stored);

        // User deletes message from history
        _repository.DeleteByRowId(stored[0].RowId, MessageDeletionSource.Feed);
        Assert.Empty(_repository.Query(topicId: topicId));

        // Live update arrives via WS (isBackfill = false) for the deleted message ID
        var msgUpdate = new NtfyMessage
        {
            Id = "msg_005",
            Topic = "test",
            Message = "Updated Body text",
            Time = timestamp
        };

        var result = _repository.Insert(msgUpdate, topicId, serverId, isBackfill: false);
        Assert.Equal(MessageSaveResult.Inserted, result);
        
        var queried = _repository.Query(topicId: topicId);
        Assert.Single(queried);
        Assert.Equal("Updated Body text", queried[0].Body);
    }

    [Fact]
    public void Insert_AfterDelete_CatchupReplay_ReturnsIgnored()
    {
        var topicId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var msg1 = new NtfyMessage
        {
            Id = "msg_006",
            Topic = "test",
            Message = "Original Body",
            Time = timestamp
        };

        _repository.Insert(msg1, topicId, serverId);
        var stored = _repository.Query(topicId: topicId);
        Assert.Single(stored);

        // User deletes message from history
        _repository.DeleteByRowId(stored[0].RowId, MessageDeletionSource.Feed);
        Assert.Empty(_repository.Query(topicId: topicId));

        // Reconnect catch-up replays boundary message (isBackfill = true)
        var msgReplay = new NtfyMessage
        {
            Id = "msg_006",
            Topic = "test",
            Message = "Original Body",
            Time = timestamp
        };

        var result = _repository.Insert(msgReplay, topicId, serverId, isBackfill: true);
        Assert.Equal(MessageSaveResult.Ignored, result);
        Assert.Empty(_repository.Query(topicId: topicId));
    }
}

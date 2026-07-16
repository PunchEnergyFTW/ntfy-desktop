using System.Net.Http;
using System.Text;
using NtfyDesktop.Domain;
using NtfyDesktop.Features.Settings;
using NtfyDesktop.Features.Topics;

namespace NtfyDesktop.Features.Feed;

public sealed class PublishService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly AppSettings _settings;

    public PublishService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task PublishAsync(
        Guid topicId,
        string body,
        string? title,
        Priority priority,
        string? tags,
        string? clickUrl,
        bool isMarkdown)
    {
        var topic = _settings.GetTopicById(topicId);
        if (topic == null)
            throw new InvalidOperationException("Topic not found.");

        var server = _settings.GetServer(topic.ServerId);
        if (server == null)
            throw new InvalidOperationException("Server not found.");

        var url = $"{server.Url.TrimEnd('/')}/{topic.Name}";

        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(title))
            queryParams.Add($"title={Uri.EscapeDataString(title.Trim())}");
        if (priority != Priority.Default)
            queryParams.Add($"priority={(int)priority}");
        if (!string.IsNullOrWhiteSpace(tags))
            queryParams.Add($"tags={Uri.EscapeDataString(tags.Trim())}");
        if (!string.IsNullOrWhiteSpace(clickUrl))
            queryParams.Add($"click={Uri.EscapeDataString(clickUrl.Trim())}");
        if (isMarkdown)
            queryParams.Add("markdown=yes");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        var authHeader = server.GetAuthorizationHeader();
        if (!string.IsNullOrEmpty(authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }

        request.Headers.UserAgent.ParseAdd("ntfy-desktop");
        request.Content = new StringContent(body, Encoding.UTF8, "text/plain");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var message = !string.IsNullOrWhiteSpace(errorBody) ? errorBody : response.ReasonPhrase;
            throw new HttpRequestException($"Publish failed ({response.StatusCode}): {message}");
        }
    }
}

using System.Net;
using System.Text;

namespace Core.Test.Payments;

/// <summary>
/// A provider's HTTP endpoint in a test: records every request (address, headers, body) and answers with the queued bodies in order, so a
/// provider package is tested without a network.
/// </summary>
public sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly Queue<string> _answers = new();

    public List<(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string Body, string? ContentType)> Requests { get; } = [];

    public RecordingHttpHandler Answer(string body)
    {
        _answers.Enqueue(body);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Dictionary<string, string> headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        Requests.Add((request.Method, request.RequestUri!, headers, body, request.Content?.Headers.ContentType?.MediaType));
        if (_answers.Count == 0)
            throw new InvalidOperationException($"No answer queued for {request.RequestUri}.");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_answers.Dequeue(), Encoding.UTF8, "application/json") };
    }
}

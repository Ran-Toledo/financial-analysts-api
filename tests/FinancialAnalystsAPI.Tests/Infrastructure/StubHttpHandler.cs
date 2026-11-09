using System.Net;
using System.Text;

namespace FinancialAnalystsAPI.Tests.Infrastructure;

/// <summary>Answers requests by path (relative to the client's base address); records every request URI.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public List<Uri> Requests { get; } = [];

    public StubHttpHandler Json(string path, string json) =>
        On(path, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    public StubHttpHandler On(string path, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes[path] = respond;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var response = _routes.TryGetValue(path, out var respond)
            ? respond(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}

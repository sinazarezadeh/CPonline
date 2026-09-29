using System.Net;

namespace CPonline.Launcher.Tests.Fakes;

/// <summary>Routes requests to a caller-supplied function instead of the network, so
/// <see cref="System.Net.Http.HttpClient"/>-based services can be tested without real HTTP calls.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHttpMessageHandler ReturningJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(json) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_responder(request));
}

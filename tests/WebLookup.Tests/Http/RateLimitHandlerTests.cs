using System.Net;
using System.Net.Http.Headers;
using WebLookup.Http;

namespace WebLookup.Tests.Http;

public class RateLimitHandlerTests
{
    [Fact]
    public async Task Send_Success_ReturnsDirectly()
    {
        var inner = new MockHttpHandler("OK");
        var handler = new RateLimitHandler(inner, maxRetries: 3);
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Send_429ThenSuccess_Retries()
    {
        var callCount = 0;
        var inner = new MockHttpHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("success")
            });
        });

        var handler = new RateLimitHandler(inner, maxRetries: 3);
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Send_429WithRetryAfter_RespectsHeader()
    {
        var callCount = 0;
        var inner = new MockHttpHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return Task.FromResult(resp);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new RateLimitHandler(inner, maxRetries: 3);
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Send_429ExceedsMaxRetries_Returns429()
    {
        var inner = new MockHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var handler = new RateLimitHandler(inner, maxRetries: 2);
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Send_429_InvokesCallback()
    {
        string? reportedProvider = null;
        var inner = new MockHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var handler = new RateLimitHandler(inner, maxRetries: 0,
            onRateLimited: (provider, _) => reportedProvider = provider);
        var client = new HttpClient(handler);

        await client.GetAsync("https://example.com/api");

        Assert.Equal("example.com", reportedProvider);
    }
}

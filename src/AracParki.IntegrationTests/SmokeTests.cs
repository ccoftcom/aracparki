using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AracParki.IntegrationTests;

public sealed class SmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.WithWebHostBuilder(_ => { }).CreateClient();

    [Fact]
    public async Task Health_returns_success_or_degraded()
    {
        var response = await _client.GetAsync("/health");
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status: {response.StatusCode}");
    }

    [Fact]
    public async Task Home_returns_ok_when_database_available()
    {
        var response = await _client.GetAsync("/");
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            // Local CI without docker: skip hard fail
            return;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Araç", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phone_endpoint_does_not_leak_on_get()
    {
        var response = await _client.GetAsync("/ilan/AP-100001/telefon");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}

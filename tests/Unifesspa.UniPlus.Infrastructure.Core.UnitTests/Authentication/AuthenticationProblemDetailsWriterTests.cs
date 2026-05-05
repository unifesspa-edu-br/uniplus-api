namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authentication;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

public sealed class AuthenticationProblemDetailsWriterTests
{
    [Fact]
    public async Task WriteUnauthorizedAsync_Should_Emit_ProblemJson_With_Canonical_Shape()
    {
        DefaultHttpContext context = CreateHttpContext(includeProblemDetailsService: true);

        await AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync(context);

        await AssertProblemDetailsAsync(
            context,
            expectedStatus: StatusCodes.Status401Unauthorized,
            expectedCode: AuthenticationProblemDetailsWriter.UnauthorizedCode);
    }

    [Fact]
    public async Task WriteForbiddenAsync_Should_Emit_ProblemJson_With_Canonical_Shape()
    {
        DefaultHttpContext context = CreateHttpContext(includeProblemDetailsService: true);

        await AuthenticationProblemDetailsWriter.WriteForbiddenAsync(context);

        await AssertProblemDetailsAsync(
            context,
            expectedStatus: StatusCodes.Status403Forbidden,
            expectedCode: AuthenticationProblemDetailsWriter.ForbiddenCode);
    }

    [Fact]
    public async Task WriteUnauthorizedAsync_Fallback_Should_Set_ProblemJson_ContentType()
    {
        // Sem IProblemDetailsService no DI — caminho de fallback manual.
        // Sentinela contra o overload de WriteAsJsonAsync que reescreve
        // Content-Type para "application/json; charset=utf-8".
        DefaultHttpContext context = CreateHttpContext(includeProblemDetailsService: false);

        await AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync(context);

        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task WriteUnauthorizedAsync_Should_Be_NoOp_When_Response_Has_Started()
    {
        DefaultHttpContext context = CreateHttpContext(includeProblemDetailsService: true);
        context.Response.StatusCode = StatusCodes.Status200OK;
        StartedResponseFeature feature = new();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(feature);

        await AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private static DefaultHttpContext CreateHttpContext(bool includeProblemDetailsService)
    {
        ServiceCollection services = new();
        services.AddOptions();
        services.AddLogging();
        if (includeProblemDetailsService)
            services.AddProblemDetails();

        ServiceProvider provider = services.BuildServiceProvider();
        DefaultHttpContext context = new()
        {
            RequestServices = provider,
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpContext context,
        int expectedStatus,
        string expectedCode)
    {
        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        context.Response.Body.Position = 0;
        using JsonDocument doc = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        root.GetProperty("type").GetString()
            .Should().Be($"https://uniplus.unifesspa.edu.br/errors/{expectedCode}");
        root.GetProperty("code").GetString().Should().Be(expectedCode);
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("instance").GetString().Should().StartWith("urn:uuid:");
    }

    private sealed class StartedResponseFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}

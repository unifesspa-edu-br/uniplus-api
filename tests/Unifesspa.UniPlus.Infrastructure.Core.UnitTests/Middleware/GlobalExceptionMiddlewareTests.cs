namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public class GlobalExceptionMiddlewareTests
{
    // ─── Fluxo sem exceção ─────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SemExcecao_DevePassarParaProximoMiddleware()
    {
        bool proximoChamado = false;
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
        {
            proximoChamado = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(CriarContexto());

        proximoChamado.Should().BeTrue();
    }

    // ─── ValidationException → 422 ────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ComValidationException_DeveRetornar422()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new ValidationException([new ValidationFailure("Email", "E-mail inválido") { ErrorCode = "Email.Invalido" }]));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_ContentTypeDeveSerApplicationProblemJson()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new ValidationException([new ValidationFailure("Campo", "Inválido") { ErrorCode = "Campo.Invalido" }]));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_DeveConterArrayDeErrorsNoFormato()
    {
        ValidationFailure falha = new("Email", "E-mail inválido") { ErrorCode = "Email.Invalido" };
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw new ValidationException([falha]));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);

        JsonElement primeiro = errors[0];
        primeiro.GetProperty("field").GetString().Should().Be("Email");
        primeiro.GetProperty("code").GetString().Should().Be("Email.Invalido");
        primeiro.GetProperty("message").GetString().Should().Be("E-mail inválido");
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_DeveConterExtensionsRfc9457()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new ValidationException([new ValidationFailure("Campo", "Erro") { ErrorCode = "Campo.Erro" }]));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.validacao");
        doc.RootElement.GetProperty("instance").GetString().Should().StartWith("urn:uuid:");

        string? traceId = doc.RootElement.GetProperty("traceId").GetString();
        traceId.Should().NotBeNullOrEmpty();
        traceId!.Length.Should().Be(32);
        Regex.IsMatch(traceId, "^[0-9a-f]{32}$").Should().BeTrue("traceId deve ser 32 hex lowercase (W3C)");
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_InstanceDeveSerUrnUuidOpaco()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new ValidationException([new ValidationFailure("Cpf", "Cpf.Invalido") { ErrorCode = "Cpf.Invalido" }]));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        string? instance = doc.RootElement.GetProperty("instance").GetString();
        instance.Should().StartWith("urn:uuid:");
        Guid.TryParse(instance!["urn:uuid:".Length..], out _).Should().BeTrue();
    }

    // ─── Exception genérica → 500 ─────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ComExcecaoGenerica_DeveRetornar500()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw new InvalidOperationException("erro interno"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_ComExcecaoGenerica_ContentTypeDeveSerApplicationProblemJson()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw new InvalidOperationException("qualquer erro"));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ComExcecaoGenerica_DetailDeveSerMensagemOpaca()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new InvalidOperationException("dado sensível do banco de dados"));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        string? detail = doc.RootElement.GetProperty("detail").GetString();
        detail.Should().NotContain("dado sensível", "a mensagem interna não deve vazar para o cliente");
        detail.Should().Be("Ocorreu um erro inesperado. Tente novamente mais tarde.");
    }

    [Fact]
    public async Task InvokeAsync_ComExcecaoGenerica_DeveConterExtensionsRfc9457()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw new InvalidOperationException("erro"));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.internal.unexpected");
        doc.RootElement.GetProperty("instance").GetString().Should().StartWith("urn:uuid:");

        string? traceId = doc.RootElement.GetProperty("traceId").GetString();
        traceId.Should().HaveLength(32);
        Regex.IsMatch(traceId!, "^[0-9a-f]{32}$").Should().BeTrue();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DefaultHttpContext CriarContexto()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static GlobalExceptionMiddleware CriarMiddleware(RequestDelegate next)
    {
        ILogger<GlobalExceptionMiddleware> logger = Substitute.For<ILogger<GlobalExceptionMiddleware>>();
        return new GlobalExceptionMiddleware(next, logger);
    }

    private static async Task<JsonDocument> LerBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}

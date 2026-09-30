namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public sealed class GlobalExceptionMiddlewareTests
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

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
            throw ExcecaoDeValidacao(ComandoValido() with { Email = "invalido" }));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_ContentTypeDeveSerApplicationProblemJson()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Nome = "" }));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_DeveConterArrayDeErrorsNoFormato()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Email = "invalido" }));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);

        JsonElement primeiro = errors[0];
        primeiro.GetProperty("field").GetString().Should().Be("email");
        primeiro.GetProperty("code").GetString().Should().Be(ValidationErrorCodes.Formato);
        primeiro.GetProperty("message").GetString().Should().Be("E-mail inválido");
    }

    /// <summary>
    /// Cada categoria de validator publica o código da taxonomia, e não o nome da classe
    /// interna do FluentValidation (<c>NotEmptyValidator</c>, <c>MaximumLengthValidator</c>…)
    /// que vazava para o contrato quando nenhuma regra declarava <c>.WithErrorCode</c>.
    /// </summary>
    [Theory]
    [InlineData(nameof(ComandoDeTeste.Nome), "", ValidationErrorCodes.Obrigatorio)]
    [InlineData(nameof(ComandoDeTeste.Nome), "nome longo demais", ValidationErrorCodes.Tamanho)]
    [InlineData(nameof(ComandoDeTeste.Email), "invalido", ValidationErrorCodes.Formato)]
    [InlineData(nameof(ComandoDeTeste.Idade), "-1", ValidationErrorCodes.Faixa)]
    [InlineData(nameof(ComandoDeTeste.Apelido), "proibido", ValidationErrorCodes.Regra)]
    public async Task InvokeAsync_ComValidationException_CodeDeCadaViolacaoVemDaTaxonomia(
        string campo, string valor, string codeEsperado)
    {
        ComandoDeTeste comando = campo switch
        {
            nameof(ComandoDeTeste.Nome) => ComandoValido() with { Nome = valor },
            nameof(ComandoDeTeste.Email) => ComandoValido() with { Email = valor },
            nameof(ComandoDeTeste.Idade) => ComandoValido() with { Idade = int.Parse(valor, System.Globalization.CultureInfo.InvariantCulture) },
            _ => ComandoValido() with { Apelido = valor },
        };
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw ExcecaoDeValidacao(comando));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetProperty("code").GetString().Should().Be(codeEsperado);
    }

    [Fact]
    public async Task InvokeAsync_ComWithErrorCodeExplicito_CodeVemDoMapeamentoDaRegra()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Cpf = "123" }));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("uniplus.cpf.invalido");
    }

    [Fact]
    public async Task InvokeAsync_ComViolacaoEmColecao_FieldEmCamelCasePorSegmentoPreservandoIndice()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Itens = [new ItemDeTeste(1), new ItemDeTeste(0)] }));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        JsonElement primeiro = doc.RootElement.GetProperty("errors")[0];
        primeiro.GetProperty("field").GetString().Should().Be("itens[1].quantidade");
        primeiro.GetProperty("code").GetString().Should().Be(ValidationErrorCodes.Faixa);
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_TitleVemDoMapper()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Nome = "" }));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Erro de validação");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task InvokeAsync_ComValidationException_DeveConterExtensionsRfc9457()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw ExcecaoDeValidacao(ComandoValido() with { Nome = "" }));

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
            throw ExcecaoDeValidacao(ComandoValido() with { Cpf = "123" }));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        string? instance = doc.RootElement.GetProperty("instance").GetString();
        instance.Should().StartWith("urn:uuid:");
        Guid.TryParse(instance!["urn:uuid:".Length..], out _).Should().BeTrue();
    }

    // ─── DbUpdateConcurrencyException → 409 (ADR-0119) ────────────────────

    [Fact]
    public async Task InvokeAsync_ComDbUpdateConcurrencyException_DeveRetornar409()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new DbUpdateConcurrencyException("conflito sintético de teste"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task InvokeAsync_ComDbUpdateConcurrencyException_ContentTypeDeveSerApplicationProblemJson()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new DbUpdateConcurrencyException("conflito sintético de teste"));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Contain("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ComDbUpdateConcurrencyException_DeveConterExtensionsRfc9457()
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ =>
            throw new DbUpdateConcurrencyException("conflito sintético de teste"));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.concorrencia.conflito");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status409Conflict);
        doc.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("instance").GetString().Should().StartWith("urn:uuid:");

        string? traceId = doc.RootElement.GetProperty("traceId").GetString();
        traceId.Should().HaveLength(32);
        Regex.IsMatch(traceId!, "^[0-9a-f]{32}$").Should().BeTrue();
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

    // ─── type aponta para o catálogo público ──────────────────────────────

    /// <summary>
    /// As três recusas que o boundary emite sozinho não passam pelo registro de erros de
    /// domínio, então o <c>type</c> delas só se liga ao catálogo aqui. Um caminho ficar
    /// para trás é um erro cujo consumidor não encontra a explicação.
    /// </summary>
    [Theory]
    [InlineData("uniplus.validacao")]
    [InlineData("uniplus.concorrencia.conflito")]
    [InlineData("uniplus.internal.unexpected")]
    public async Task InvokeAsync_QualquerRecusaDoBoundary_TypeUsaBaseDoCatalogo(string codeEsperado)
    {
        DefaultHttpContext context = CriarContexto();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw ExcecaoPara(codeEsperado));

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await LerBodyAsync(context);
        doc.RootElement.GetProperty("type").GetString()
            .Should().Be(BaseUriDoCatalogo + codeEsperado);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static Exception ExcecaoPara(string code) => code switch
    {
        "uniplus.validacao" => ExcecaoDeValidacao(ComandoValido() with { Nome = "" }),
        "uniplus.concorrencia.conflito" => new DbUpdateConcurrencyException("conflito"),
        _ => new InvalidOperationException("erro"),
    };

    private static ComandoDeTeste ComandoValido() =>
        new("Nome", "pessoa@exemplo.com", 30, "apelido", "52998224725", [new ItemDeTeste(1)]);

    // Exceção montada pelo validator real, como o pipeline do Wolverine faz: o ErrorCode de
    // cada falha é o que o resolver global produz, e não um valor escrito à mão no teste.
    private static ValidationException ExcecaoDeValidacao(ComandoDeTeste comando)
    {
        ValidationErrorCodes.InstallGlobalResolver();
        ValidationResult resultado = new ComandoDeTesteValidator().Validate(comando);
        resultado.IsValid.Should().BeFalse("o cenário precisa de ao menos uma violação");
        return new ValidationException(resultado.Errors);
    }

    private static DefaultHttpContext CriarContexto()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static GlobalExceptionMiddleware CriarMiddleware(RequestDelegate next)
    {
        ILogger<GlobalExceptionMiddleware> logger = Substitute.For<ILogger<GlobalExceptionMiddleware>>();
        ProblemTypeUriFactory problemTypeUriFactory = new(
            Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo }));
        DomainErrorMappingRegistry mapper = new([new KernelDomainErrorRegistration()], problemTypeUriFactory);
        return new GlobalExceptionMiddleware(next, logger, problemTypeUriFactory, mapper);
    }

    private static async Task<JsonDocument> LerBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    public sealed record ItemDeTeste(int Quantidade);

    public sealed record ComandoDeTeste(
        string Nome,
        string Email,
        int Idade,
        string Apelido,
        string Cpf,
        IReadOnlyList<ItemDeTeste> Itens);

    private sealed class ComandoDeTesteValidator : AbstractValidator<ComandoDeTeste>
    {
        public ComandoDeTesteValidator()
        {
            RuleFor(c => c.Nome).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(10);
            RuleFor(c => c.Email).EmailAddress().WithMessage("E-mail inválido");
            RuleFor(c => c.Idade).GreaterThanOrEqualTo(0);
            RuleFor(c => c.Apelido).Must(a => a != "proibido");
            RuleFor(c => c.Cpf).Length(11).WithErrorCode("Cpf.Invalido");
            RuleForEach(c => c.Itens).ChildRules(item => item.RuleFor(i => i.Quantidade).GreaterThan(0));
        }
    }
}

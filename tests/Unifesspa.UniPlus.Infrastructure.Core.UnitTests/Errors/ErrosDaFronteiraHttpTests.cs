namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os erros que a fronteira HTTP emite sozinha saem das definições de <see cref="ErrosDaFronteiraHttp"/>,
/// e é o registro delas que alimenta o catálogo público de códigos de erro. Cada emissor é executado
/// de verdade e o que ele escreve precisa estar nesse registro, com o mesmo status e título.
/// </summary>
public sealed class ErrosDaFronteiraHttpTests
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    public static TheoryData<string> CodigosDoMiddleware() =>
    [
        ErrosDaFronteiraHttp.ConflitoDeConcorrencia.Code,
        ErrosDaFronteiraHttp.CursorInvalido.Code,
        ErrosDaFronteiraHttp.RegistroDeAcessoNaoGravado.Code,
        ErrosDaFronteiraHttp.CatalogoDePermissoesDesatualizado.Code,
        ErrosDaFronteiraHttp.ErroInesperado.Code,
    ];

    private static IEnumerable<DomainErrorMapping> Catalogo() =>
        new ErrosDaFronteiraHttpDomainErrorRegistration().GetMappings().Select(static par => par.Value);

    [Fact(DisplayName = "Cada código de fio da fronteira é declarado uma só vez")]
    public void Registro_NaoRepeteCodigoDeFio()
    {
        Catalogo().Select(static d => d.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "O mapeador do host resolve os códigos da fronteira")]
    public void AddDomainErrorMapper_ResolveOsCodigosDaFronteira()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ProblemTypeOptions.SectionName}:BaseUri"] = BaseUriDoCatalogo })
            .Build();
        using ServiceProvider provider = new ServiceCollection().AddDomainErrorMapper(configuracao).BuildServiceProvider();
        IDomainErrorMapper mapeador = provider.GetRequiredService<IDomainErrorMapper>();

        foreach (DomainErrorMapping definicao in Catalogo())
        {
            mapeador.TryGetMapping(definicao.Code, out DomainErrorMapping? resolvido).Should().BeTrue(definicao.Code);
            resolvido.Should().Be(definicao);
        }
    }

    [Theory(DisplayName = "O middleware escreve status, código e título que estão no registro da fronteira")]
    [MemberData(nameof(CodigosDoMiddleware))]
    public async Task Middleware_EscreveDefinicaoDoRegistro(string codigo)
    {
        DefaultHttpContext contexto = new();
        contexto.Response.Body = new MemoryStream();
        GlobalExceptionMiddleware middleware = CriarMiddleware(_ => throw ExcecaoQueProduz(codigo));

        await middleware.InvokeAsync(contexto);

        using JsonDocument corpo = await LerCorpoAsync(contexto);
        AssertEstaNoRegistro(corpo.RootElement.GetProperty("status").GetInt32(), corpo.RootElement.GetProperty("code").GetString(), corpo.RootElement.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "O 401 da autenticação escreve a definição do registro da fronteira")]
    public async Task Autenticacao_NaoAutenticadoEscreveDefinicaoDoRegistro()
    {
        DefaultHttpContext contexto = CriarContextoComFabricaDeTipo();

        await AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync(contexto);

        using JsonDocument corpo = await LerCorpoAsync(contexto);
        AssertEstaNoRegistro(contexto.Response.StatusCode, corpo.RootElement.GetProperty("code").GetString(), corpo.RootElement.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "O 403 da autorização escreve a definição do registro da fronteira")]
    public async Task Autenticacao_AcessoNegadoEscreveDefinicaoDoRegistro()
    {
        DefaultHttpContext contexto = CriarContextoComFabricaDeTipo();

        await AuthenticationProblemDetailsWriter.WriteForbiddenAsync(contexto);

        using JsonDocument corpo = await LerCorpoAsync(contexto);
        AssertEstaNoRegistro(contexto.Response.StatusCode, corpo.RootElement.GetProperty("code").GetString(), corpo.RootElement.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "O 406 da negociação de versão escreve a definição do registro da fronteira")]
    public void NegociacaoDeVersao_EscreveDefinicaoDoRegistro()
    {
        ActionExecutingContext contexto = CriarContextoDeAcao("application/vnd.uniplus.edital.v2+json");

        new VendorMediaTypeAttribute { Resource = "edital", Versions = [1] }.OnActionExecuting(contexto);

        ProblemDetails problema = contexto.Result.Should().BeOfType<ObjectResult>().Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        AssertEstaNoRegistro(problema.Status!.Value, (string?)problema.Extensions["code"], problema.Title);
    }

    [Theory(DisplayName = "A resposta da requisição sem operação escreve a definição do registro da fronteira")]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed)]
    public async Task RequisicaoSemOperacao_EscreveDefinicaoDoRegistro(int status)
    {
        DefaultHttpContext contexto = await PipelineDaRequisicaoSemOperacao.ExecutarAsync(status, endpoint: null);

        using JsonDocument corpo = await LerCorpoAsync(contexto);
        AssertEstaNoRegistro(contexto.Response.StatusCode, corpo.RootElement.GetProperty("code").GetString(), corpo.RootElement.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "O código de domínio não mapeado responde com a definição do registro da fronteira")]
    public void CodigoNaoMapeado_ResolveParaADefinicaoDoRegistro()
    {
        IDomainErrorMapper mapeador = Substitute.For<IDomainErrorMapper>();
        mapeador.GetProblemTypeUri(Arg.Any<string>()).Returns(static chamada => BaseUriDoCatalogo + chamada.Arg<string>());

        (int status, _, string titulo, string codigo, _) = DomainErrorProblemDetailsFactory.Resolve(new DomainError("Codigo.NaoMapeado", "Erro."), mapeador);

        AssertEstaNoRegistro(status, codigo, titulo);
    }

    private static void AssertEstaNoRegistro(int status, string? codigo, string? titulo) =>
        Catalogo().Should().Contain(
            definicao => definicao.Status == status && definicao.Code == codigo && definicao.Title == titulo,
            "o emissor escreveu {0} {1} \"{2}\", que o catálogo público de códigos de erro não conhece", status, codigo, titulo);

    private static Exception ExcecaoQueProduz(string codigo) => codigo switch
    {
        _ when codigo == ErrosDaFronteiraHttp.ConflitoDeConcorrencia.Code => new DbUpdateConcurrencyException("conflito"),
        _ when codigo == ErrosDaFronteiraHttp.CursorInvalido.Code => new CursorAnchorMismatchException("âncora"),
        _ when codigo == ErrosDaFronteiraHttp.RegistroDeAcessoNaoGravado.Code => new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("causa")),
        _ when codigo == ErrosDaFronteiraHttp.CatalogoDePermissoesDesatualizado.Code => new CatalogoDesatualizadoException("catálogo"),
        _ => new InvalidOperationException("erro"),
    };

    private static GlobalExceptionMiddleware CriarMiddleware(RequestDelegate proximo)
    {
        ProblemTypeUriFactory fabrica = new(Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo }));
        DomainErrorMappingRegistry mapeador = new([new KernelDomainErrorRegistration()], fabrica);
        return new GlobalExceptionMiddleware(proximo, Substitute.For<ILogger<GlobalExceptionMiddleware>>(), fabrica, mapeador);
    }

    private static DefaultHttpContext CriarContextoComFabricaDeTipo()
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton<IProblemTypeUriFactory>(new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo })));
        DefaultHttpContext contexto = new() { RequestServices = servicos.BuildServiceProvider() };
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    private static ActionExecutingContext CriarContextoDeAcao(string accept)
    {
        DefaultHttpContext http = CriarContextoComFabricaDeTipo();
        http.Request.Headers.Accept = accept;
        ActionContext acao = new(http, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(acao, [], new Dictionary<string, object?>(), controller: new object());
    }

    private static async Task<JsonDocument> LerCorpoAsync(HttpContext contexto)
    {
        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(contexto.Response.Body);
    }
}

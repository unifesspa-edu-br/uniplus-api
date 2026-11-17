namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A ordem em que <c>AddDomainErrorMapper</c> e <c>AddCursorPagination</c> instalam suas
/// respostas decide quem responde a cada recusa: o erro de cursor primeiro, a leitura da
/// requisição em seguida, e o que não é de nenhum dos dois volta ao que o framework já fazia.
/// </summary>
public sealed class CadeiaDeRespostaDeLeituraInvalidaTests
{
    private const string BaseDoCatalogo = "https://exemplo.invalid/erros/";

    private static readonly ObjectResult RespostaDoFramework = new("resposta do framework");

    [Theory]
    [InlineData(CursorBindingErrorCodes.Invalido, StatusCodes.Status400BadRequest)]
    [InlineData(CursorBindingErrorCodes.Expirado, StatusCodes.Status410Gone)]
    public void InvalidModelStateResponseFactory_ErroDeCursor_RespondeOProblemaDoCursor(string codigo, int statusEsperado)
    {
        using ServiceProvider provedor = ConstruirProvedorComAsDuasExtensoes();
        ActionContext contexto = Contexto(provedor);
        contexto.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey] = codigo;
        contexto.ModelState.AddModelError("cursor", "Cursor ilegível.");

        IActionResult resposta = ResponderComAFabricaDoMvc(provedor, contexto);

        ProblemDetails problema = ProblemaDe(resposta);
        problema.Status.Should().Be(statusEsperado);
        problema.Detail.Should().Be("Cursor ilegível.");
    }

    [Fact]
    public void InvalidModelStateResponseFactory_ErroDeCursorEDeLeitura_RespondeOCursor()
    {
        using ServiceProvider provedor = ConstruirProvedorComAsDuasExtensoes();
        ActionContext contexto = Contexto(provedor);
        contexto.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey] = CursorBindingErrorCodes.Invalido;
        contexto.ModelState.AddModelError("$", "'x' is an invalid start of a value.");

        IActionResult resposta = ResponderComAFabricaDoMvc(provedor, contexto);

        ProblemaDe(resposta).Extensions["code"].Should().Be("uniplus.cursor.invalido");
    }

    [Fact]
    public void InvalidModelStateResponseFactory_ErroGenericoDeLeitura_RespondeOProblemaDeLeitura()
    {
        using ServiceProvider provedor = ConstruirProvedorComAsDuasExtensoes();
        ActionContext contexto = Contexto(provedor);
        contexto.ModelState.AddModelError("$", "'x' is an invalid start of a value.");

        IActionResult resposta = ResponderComAFabricaDoMvc(provedor, contexto);

        ProblemDetails problema = ProblemaDe(resposta);
        problema.Status.Should().Be(StatusCodes.Status400BadRequest);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.malformada");
    }

    [Fact]
    public void InvalidModelStateResponseFactory_ErroDeValidacaoDeModelo_DelegaAoFactoryDoFramework()
    {
        using ServiceProvider provedor = ConstruirProvedorComAsDuasExtensoes();
        ActionContext contexto = Contexto(provedor);
        contexto.ModelState.SetModelValue("codigo", rawValue: "edital_abertura", attemptedValue: "edital_abertura");
        contexto.ModelState.AddModelError("codigo", "Código deve usar apenas letras maiúsculas.");

        IActionResult resposta = ResponderComAFabricaDoMvc(provedor, contexto);

        resposta.Should().BeSameAs(RespostaDoFramework);
    }

    [Fact]
    public void InvalidModelStateResponseFactory_SoAddDomainErrorMapper_ErroDeCursorSegueParaOFactoryDoFramework()
    {
        ServiceCollection services = ConfigurarFactoryDoFramework();
        services.AddDomainErrorMapper(ConfiguracaoComBaseDoCatalogo());
        using ServiceProvider provedor = services.BuildServiceProvider();
        ActionContext contexto = Contexto(provedor);
        contexto.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey] = CursorBindingErrorCodes.Invalido;

        IActionResult resposta = ResponderComAFabricaDoMvc(provedor, contexto);

        resposta.Should().BeSameAs(RespostaDoFramework);
    }

    [Fact]
    public void MvcOptions_ProviderSemBinderInterno_DevolveNulo()
    {
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(binderInterno: null, new FormatterDeTeste());
        MvcOptions opcoes = provedor.GetRequiredService<IOptions<MvcOptions>>().Value;

        IModelBinder? binder = opcoes.ModelBinderProviders[0].GetBinder(Substitute.For<ModelBinderProviderContext>());

        binder.Should().BeNull();
    }

    [Fact]
    public async Task MvcOptions_BinderQueFalhaNaConversao_FazAFabricaResponderComOProblemaDoParametro()
    {
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(
            new BinderDeTeste(RecusarValorInformado),
            new FormatterDeTeste());
        ActionContext contexto = ContextoComParametroDeQuery(provedor, "vigentes");

        await ExecutarBinderEnvolvidoAsync(provedor, contexto, "vigentes");

        ProblemDetails problema = ProblemaDe(ResponderComAFabricaDoMvc(provedor, contexto));
        problema.Status.Should().Be(StatusCodes.Status400BadRequest);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.valor_invalido");
        problema.Detail.Should().Contain("vigentes");
    }

    [Fact]
    public async Task MvcOptions_BinderQueConverte_NaoAnotaFalhaDeConversao()
    {
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(
            new BinderDeTeste(_ => { }),
            new FormatterDeTeste());
        ActionContext contexto = ContextoComParametroDeQuery(provedor, "vigentes");

        await ExecutarBinderEnvolvidoAsync(provedor, contexto, "vigentes");

        ValueConversionFailures.Of(contexto.HttpContext).Should().BeEmpty();
    }

    [Fact]
    public async Task MvcOptions_FormatterJsonComUniaoSemDiscriminador_RecusaALeituraSemLancar()
    {
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(null, new FormatterDeTeste());
        IInputFormatter formatter = provedor.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters[0];
        InputFormatterContext contexto = ContextoDeLeitura("""{"predicado":{}}""");

        InputFormatterResult resultado = await formatter.ReadAsync(contexto);

        resultado.HasError.Should().BeTrue();
        contexto.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task MvcOptions_FormatterJsonComCorpoValido_LeOModeloSemInterferencia()
    {
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(null, new FormatterDeTeste());
        IInputFormatter formatter = provedor.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters[0];
        InputFormatterContext contexto = ContextoDeLeitura("""{"predicado":{"$tipo":"etapa","codigo":"PROVA_OBJETIVA"}}""");

        formatter.CanRead(contexto).Should().BeTrue();
        InputFormatterResult resultado = await formatter.ReadAsync(contexto);

        resultado.Model.Should().BeOfType<ComandoDeTeste>()
            .Which.Predicado.Should().BeOfType<EtapaDeTeste>()
            .Which.Codigo.Should().Be("PROVA_OBJETIVA");
    }

    [Fact]
    public void MvcOptions_FormatterDeOutroTipo_PermanecePeloMesmoObjetoENaMesmaPosicao()
    {
        FormatterDeTeste formatterDeOutroTipo = new();
        using ServiceProvider provedor = ConstruirProvedorComBindersEFormatters(null, formatterDeOutroTipo);

        MvcOptions opcoes = provedor.GetRequiredService<IOptions<MvcOptions>>().Value;

        opcoes.ModelBinderProviders.Should().HaveCount(2)
            .And.AllBeOfType<ValueConversionTrackingModelBinderProvider>();
        opcoes.InputFormatters.Should().HaveCount(2);
        opcoes.InputFormatters[0].Should().BeOfType<PolymorphicDeserializationInputFormatter>();
        opcoes.InputFormatters[1].Should().BeSameAs(formatterDeOutroTipo);
    }

    private static ServiceProvider ConstruirProvedorComBindersEFormatters(
        IModelBinder? binderInterno, IInputFormatter formatterDeOutroTipo)
    {
        ServiceCollection services = ConfigurarFactoryDoFramework();
        services.Configure<MvcOptions>(opcoes =>
        {
            opcoes.ModelBinderProviders.Add(new ModelBinderProviderDeTeste(null));
            opcoes.ModelBinderProviders.Add(new ModelBinderProviderDeTeste(binderInterno));
            opcoes.InputFormatters.Add(new SystemTextJsonInputFormatter(
                new JsonOptions { JsonSerializerOptions = { PropertyNamingPolicy = JsonNamingPolicy.CamelCase } },
                NullLogger<SystemTextJsonInputFormatter>.Instance));
            opcoes.InputFormatters.Add(formatterDeOutroTipo);
        });
        IConfiguration configuracao = ConfiguracaoComBaseDoCatalogo();
        services.AddDomainErrorMapper(configuracao);
        services.AddCursorPagination(configuracao);
        return services.BuildServiceProvider();
    }

    private static void RecusarValorInformado(ModelBindingContext contexto)
    {
        contexto.ModelState.SetModelValue(contexto.ModelName, rawValue: "abc", attemptedValue: "abc");
        contexto.ModelState.AddModelError(contexto.ModelName, "The value 'abc' is not valid.");
    }

    private static ActionContext ContextoComParametroDeQuery(IServiceProvider provedor, string nomeDoParametro)
    {
        ParameterDescriptor parametro = new()
        {
            Name = nomeDoParametro,
            BindingInfo = new BindingInfo { BindingSource = BindingSource.Query },
        };
        return new ActionContext(
            new DefaultHttpContext { RequestServices = provedor },
            new RouteData(),
            new ActionDescriptor { Parameters = [parametro] });
    }

    private static async Task ExecutarBinderEnvolvidoAsync(
        IServiceProvider provedor, ActionContext contexto, string nomeDoParametro)
    {
        MvcOptions opcoes = provedor.GetRequiredService<IOptions<MvcOptions>>().Value;
        IModelBinder binder = opcoes.ModelBinderProviders[1].GetBinder(Substitute.For<ModelBinderProviderContext>())!;
        DefaultModelBindingContext bindingContext = new()
        {
            ActionContext = contexto,
            ModelName = nomeDoParametro,
            ModelState = contexto.ModelState,
            BindingSource = BindingSource.Query,
        };

        await binder.BindModelAsync(bindingContext);
    }

    private static InputFormatterContext ContextoDeLeitura(string corpo)
    {
        DefaultHttpContext http = new();
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(corpo));

        return new InputFormatterContext(
            http,
            modelName: string.Empty,
            new ModelStateDictionary(),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(ComandoDeTeste)),
            static (stream, encoding) => new StreamReader(stream, encoding));
    }

    private static ServiceCollection ConfigurarFactoryDoFramework()
    {
        ServiceCollection services = new();
        services.Configure<ApiBehaviorOptions>(
            opcoes => opcoes.InvalidModelStateResponseFactory = _ => RespostaDoFramework);
        return services;
    }

    private static IConfiguration ConfiguracaoComBaseDoCatalogo() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProblemTypeOptions.SectionName}:BaseUri"] = BaseDoCatalogo,
            })
            .Build();

    private static ServiceProvider ConstruirProvedorComAsDuasExtensoes()
    {
        ServiceCollection services = ConfigurarFactoryDoFramework();
        IConfiguration configuracao = ConfiguracaoComBaseDoCatalogo();
        services.AddDomainErrorMapper(configuracao);
        services.AddCursorPagination(configuracao);
        return services.BuildServiceProvider();
    }

    private static ActionContext Contexto(IServiceProvider provedor) =>
        new(
            new DefaultHttpContext { RequestServices = provedor },
            new RouteData(),
            new ActionDescriptor { Parameters = [] });

    private static IActionResult ResponderComAFabricaDoMvc(IServiceProvider provedor, ActionContext contexto) =>
        provedor.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory(contexto);

    private static ProblemDetails ProblemaDe(IActionResult resposta) =>
        (ProblemDetails)resposta.Should().BeOfType<ObjectResult>().Subject.Value!;

    private sealed class ModelBinderProviderDeTeste(IModelBinder? binder) : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context) => binder;
    }

    private sealed class BinderDeTeste(Action<ModelBindingContext> acao) : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            acao(bindingContext);
            return Task.CompletedTask;
        }
    }

    private sealed class FormatterDeTeste : IInputFormatter
    {
        public bool CanRead(InputFormatterContext context) => false;

        public Task<InputFormatterResult> ReadAsync(InputFormatterContext context) =>
            InputFormatterResult.NoValueAsync();
    }

    private sealed record ComandoDeTeste(PredicadoDeTeste Predicado);

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
    [JsonDerivedType(typeof(EtapaDeTeste), "etapa")]
    private abstract record PredicadoDeTeste;

    private sealed record EtapaDeTeste(string Codigo) : PredicadoDeTeste;
}

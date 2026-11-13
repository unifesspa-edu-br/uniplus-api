namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Instantes;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class InstanteEmMicrossegundosModelBinderProviderTests
{
    private const string Chave = "instante";

    private static readonly DateTimeOffset AberturaEmUtc = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Instante da query com 7 casas é entregue truncado em 6, preservando o offset")]
    public async Task Query_SetimaCasa_EntregaTruncado()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(DateTimeOffset), BindingSource.Query, Query("2026-11-12T10:00:00.9999999-03:00"));

        contexto.Result.IsModelSet.Should().BeTrue();
        DateTimeOffset instante = (DateTimeOffset)contexto.Result.Model!;
        instante.Offset.Should().Be(TimeSpan.FromHours(-3));
        instante.Ticks.Should().Be(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)).AddTicks(9_999_990).Ticks);
    }

    [Fact(DisplayName = "Instante anulável da query com 7 casas é entregue truncado")]
    public async Task QueryAnulavel_SetimaCasa_EntregaTruncado()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(DateTimeOffset?), BindingSource.Query, Query("2026-11-12T10:00:00.9999999Z"));

        contexto.Result.Model.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
    }

    [Theory(DisplayName = "Instante da query que não é data e hora vai para o ModelState e não vira valor")]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateTimeOffset?))]
    public async Task Query_Invalido_RegistraErroNoModelState(Type tipo)
    {
        ModelBindingContext contexto = await VincularAsync(tipo, BindingSource.Query, Query("amanhã de manhã"));

        contexto.Result.IsModelSet.Should().BeFalse();
        contexto.ModelState[Chave]!.Errors.Should().ContainSingle();
    }

    [Theory(DisplayName = "Instante ausente da query fica sem valor e sem erro")]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateTimeOffset?))]
    public async Task Query_Ausente_FicaSemValor(Type tipo)
    {
        ModelBindingContext contexto = await VincularAsync(tipo, BindingSource.Query, Query());

        contexto.Result.IsModelSet.Should().BeFalse();
        contexto.ModelState.ErrorCount.Should().Be(0);
    }

    [Fact(DisplayName = "Instante anulável enviado vazio chega como nulo, sem erro")]
    public async Task QueryAnulavel_Vazio_ChegaComoNulo()
    {
        ModelBindingContext contexto = await VincularAsync(typeof(DateTimeOffset?), BindingSource.Query, Query(string.Empty));

        contexto.Result.IsModelSet.Should().BeTrue();
        contexto.Result.Model.Should().BeNull();
        contexto.ModelState.ErrorCount.Should().Be(0);
    }

    [Fact(DisplayName = "Instante da rota com 7 casas é entregue truncado")]
    public async Task Rota_SetimaCasa_EntregaTruncado()
    {
        RouteValueProvider rota = new(
            BindingSource.Path, new RouteValueDictionary { [Chave] = "2026-11-12T10:00:00.9999999Z" }, CultureInfo.InvariantCulture);

        ModelBindingContext contexto = await VincularAsync(typeof(DateTimeOffset), BindingSource.Path, rota);

        contexto.Result.Model.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
    }

    [Fact(DisplayName = "Instante da rota que não é data e hora vai para o ModelState")]
    public async Task Rota_Invalido_RegistraErroNoModelState()
    {
        RouteValueProvider rota = new(BindingSource.Path, new RouteValueDictionary { [Chave] = "abc" }, CultureInfo.InvariantCulture);

        ModelBindingContext contexto = await VincularAsync(typeof(DateTimeOffset), BindingSource.Path, rota);

        contexto.Result.IsModelSet.Should().BeFalse();
        contexto.ModelState[Chave]!.Errors.Should().ContainSingle();
    }

    [Fact(DisplayName = "A conversão segue a cultura do provedor de valores, não a da thread")]
    public async Task Query_UsaACulturaDoProvedor()
    {
        QueryStringValueProvider brasileiro = new(
            BindingSource.Query, Colecao("12/11/2026 10:00:00.9999999 +00:00"), new CultureInfo("pt-BR"));

        ModelBindingContext contexto = await VincularAsync(typeof(DateTimeOffset), BindingSource.Query, brasileiro);

        contexto.Result.Model.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
    }

    [Fact(DisplayName = "Instante do cabeçalho com 7 casas é entregue truncado")]
    public async Task Cabecalho_SetimaCasa_EntregaTruncado()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(DateTimeOffset), BindingSource.Header, Query(), cabecalho: "2026-11-12T10:00:00.9999999Z");

        contexto.Result.Model.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
    }

    [Fact(DisplayName = "Lista de instantes da query chega com cada elemento truncado")]
    public async Task QueryColecao_TruncaCadaElemento()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(IReadOnlyList<DateTimeOffset>),
            BindingSource.Query,
            Query("2026-11-12T10:00:00.0000009Z", "2026-11-12T10:00:00.0000019Z"));

        contexto.Result.Model.Should().BeAssignableTo<IEnumerable<DateTimeOffset>>()
            .Which.Should().Equal(AberturaEmUtc, AberturaEmUtc.AddTicks(10));
    }

    [Fact(DisplayName = "Lista de instantes da query com elemento inválido registra o erro desse elemento")]
    public async Task QueryColecao_ElementoInvalido_RegistraErro()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(IReadOnlyList<DateTimeOffset>), BindingSource.Query, Query("2026-11-12T10:00:00Z", "abc"));

        contexto.ModelState.ErrorCount.Should().Be(1);
        contexto.ModelState.Keys.Should().ContainSingle().Which.Should().StartWith(Chave);
    }

    [Fact(DisplayName = "Lista de instantes anuláveis da query aceita elemento vazio como nulo e trunca os demais")]
    public async Task QueryColecaoAnulavel_ElementoVazioViraNulo()
    {
        ModelBindingContext contexto = await VincularAsync(
            typeof(IReadOnlyList<DateTimeOffset?>), BindingSource.Query, Query("2026-11-12T10:00:00.0000019Z", string.Empty));

        contexto.Result.Model.Should().BeAssignableTo<IEnumerable<DateTimeOffset?>>()
            .Which.Should().Equal(AberturaEmUtc.AddTicks(10), null);
    }

    private static QueryStringValueProvider Query(params string[] valores) =>
        new(BindingSource.Query, Colecao(valores), CultureInfo.InvariantCulture);

    private static QueryCollection Colecao(params string[] valores) =>
        valores.Length == 0 ? new() : new(new Dictionary<string, StringValues> { [Chave] = valores });

    private static async Task<ModelBindingContext> VincularAsync(
        Type tipo, BindingSource fonte, IValueProvider provedor, string? cabecalho = null)
    {
        await using ServiceProvider raiz = ServicosDoHost();
        ModelMetadata metadata = raiz.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(tipo);
        BindingInfo info = new() { BindingSource = fonte };
        IModelBinder binder = raiz.GetRequiredService<IModelBinderFactory>()
            .CreateBinder(new ModelBinderFactoryContext { Metadata = metadata, BindingInfo = info, CacheToken = tipo });
        ModelBindingContext contexto = DefaultModelBindingContext.CreateBindingContext(
            AcaoComCabecalho(raiz, cabecalho), provedor, metadata, info, Chave);

        await binder.BindModelAsync(contexto);
        return contexto;
    }

    private static ServiceProvider ServicosDoHost()
    {
        ServiceCollection servicos = new();
        servicos.AddLogging().AddMvcCore();
        servicos.AddUniPlusJsonOptions();

        return servicos.BuildServiceProvider();
    }

    private static ActionContext AcaoComCabecalho(IServiceProvider servicos, string? cabecalho)
    {
        DefaultHttpContext http = new() { RequestServices = servicos };
        http.Request.Headers[Chave] = cabecalho;

        return new ActionContext(http, new RouteData(), new ActionDescriptor());
    }
}

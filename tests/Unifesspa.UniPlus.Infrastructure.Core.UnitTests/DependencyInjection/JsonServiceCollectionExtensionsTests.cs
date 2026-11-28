namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Instantes;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class JsonServiceCollectionExtensionsTests
{
    private const string InstanteComSetimaCasa = "\"2026-11-12T10:00:00.9999999Z\"";

    // O código declarado de ModoDeEntregaDeTeste.Postal, que difere do nome, do nome em camelCase e do número do membro.
    private const string CodigoDoModoPostalComoTexto = "\"PELO_CORREIO\"";

    private static readonly DateTimeOffset InstanteEsperado = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero).AddTicks(9_999_990);

    [Fact(DisplayName = "Os controllers leem o instante do corpo truncado em microssegundos")]
    public void Controllers_LemInstanteTruncado()
    {
        using ServiceProvider provedor = Provedor();

        JsonSerializerOptions opcoes = provedor.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        JsonSerializer.Deserialize<DateTimeOffset>(InstanteComSetimaCasa, opcoes).Should().Be(InstanteEsperado);
    }

    [Fact(DisplayName = "As Minimal APIs leem o instante do corpo truncado em microssegundos")]
    public void MinimalApis_LemInstanteTruncado()
    {
        using ServiceProvider provedor = Provedor();

        JsonSerializerOptions opcoes = provedor.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;

        JsonSerializer.Deserialize<DateTimeOffset?>(InstanteComSetimaCasa, opcoes).Should().Be(InstanteEsperado);
    }

    [Fact(DisplayName = "Os controllers não escrevem o enum de contrato pelo código: a resposta HTTP não usa o conversor de código")]
    public void Controllers_EnumComCodigo_NaoSaiPeloCodigo()
    {
        using ServiceProvider provedor = Provedor();
        JsonSerializerOptions opcoes = provedor.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        JsonSerializer.Serialize(ModoDeEntregaDeTeste.Postal, opcoes).Should().NotBe(CodigoDoModoPostalComoTexto);
    }

    [Fact(DisplayName = "As Minimal APIs não escrevem o enum de contrato pelo código: a resposta HTTP não usa o conversor de código")]
    public void MinimalApis_EnumComCodigo_NaoSaiPeloCodigo()
    {
        using ServiceProvider provedor = Provedor();
        JsonSerializerOptions opcoes = provedor.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;

        JsonSerializer.Serialize(ModoDeEntregaDeTeste.Postal, opcoes).Should().NotBe(CodigoDoModoPostalComoTexto);
    }

    [Fact(DisplayName = "O binder de instante vem antes do binder de tipo simples do MVC")]
    public void BinderDeInstante_VemAntesDoBinderSimples()
    {
        using ServiceProvider provedor = Provedor();

        IList<IModelBinderProvider> binders = provedor.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders;

        binders.OfType<InstanteEmMicrossegundosModelBinderProvider>().Should().ContainSingle();
        binders.Select(static b => b.GetType()).ToList().Should().ContainInOrder(
            typeof(InstanteEmMicrossegundosModelBinderProvider), typeof(SimpleTypeModelBinderProvider));
    }

    [Fact(DisplayName = "O envio simulado sem instante é lido com o instante ausente, para o domínio recusá-lo")]
    public void EnvioSimulado_SemInstante_ChegaAoDominioSemInstante()
    {
        using ServiceProvider provedor = Provedor();
        JsonSerializerOptions opcoes = provedor.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        EnvioSimuladoInput? lido = JsonSerializer.Deserialize<EnvioSimuladoInput>("{\"fuso\":\"America/Belem\"}", opcoes);

        lido.Should().NotBeNull();
        lido!.Instante.Should().BeNull();
    }

    private static ServiceProvider Provedor()
    {
        ServiceCollection servicos = new();
        servicos.AddLogging().AddMvcCore();
        servicos.AddUniPlusJsonOptions();

        return servicos.BuildServiceProvider();
    }
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// O registro publicado pelo endpoint, lido do banco: cada fase do cronograma grava a chave da marca "no mesmo momento da anterior",
/// marcada ou não, e a leitura do registro devolve a ordem e a marca de cada fase como foram publicadas, inclusive na versão
/// criada pela retificação.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class MarcaNoRegistroPublicadoPeloEndpointTests(CascadingFixture fixture)
{
    private const string Chave = "noMesmoMomentoDaAnterior";

    [Fact(DisplayName = "O registro publicado guarda a chave da marca em toda fase e a leitura devolve a ordem e a marca de cada uma")]
    public async Task RegistroPublicado_GuardaAMarcaEDevolveAOrdem()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Registro com a marca", 1, Criar("ENSALAMENTO", 2, 3, noMesmoMomentoDaAnterior: true), Criar("CLASSIFICACAO", 3, 4));

        using HttpResponseMessage publicada = await processo.PublicarAsync();

        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        VersaoConfiguracao versao = await processo.UltimaVersaoPublicadaAsync();
        FasesDoRegistro(versao).Should().Equal(("RESULTADO_FINAL", 1, false), ("ENSALAMENTO", 2, true), ("CLASSIFICACAO", 3, false));
        ChavesDaMarcaNoRegistro(versao).Should().Be(3, "a escrita grava a chave em toda fase, marcada ou não");
    }

    [Fact(DisplayName = "A versão criada pela retificação guarda a marca da versão anterior no mesmo lugar")]
    public async Task VersaoDaRetificacao_GuardaAMarcaNoMesmoLugar()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Registro retificado com a marca", 1, Criar("ENSALAMENTO", 2, 3, noMesmoMomentoDaAnterior: true));
        using HttpResponseMessage publicada = await processo.PublicarAsync();
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage retificada = await processo.RetificarAsync();

        retificada.StatusCode.Should().Be(HttpStatusCode.NoContent, await retificada.Content.ReadAsStringAsync());
        VersaoConfiguracao versao = await processo.UltimaVersaoPublicadaAsync();
        versao.NumeroVersao.Should().Be(2);
        FasesDoRegistro(versao).Should().Equal(("RESULTADO_FINAL", 1, false), ("ENSALAMENTO", 2, true));
    }

    private static (string Codigo, int Ordem, bool Marca)[] FasesDoRegistro(VersaoConfiguracao versao)
    {
        EnvelopeReidratado lido = ApoioDoEnvelopeCodec.Reidratado(versao);

        return [.. lido.Configuracao.Cronograma.CronogramaFases.Select(static fase => (fase.Codigo, fase.Ordem, fase.NoMesmoMomentoDaAnterior))];
    }

    private static int ChavesDaMarcaNoRegistro(VersaoConfiguracao versao)
    {
        using JsonDocument registro = JsonDocument.Parse(versao.ConfiguracaoCongelada);

        return Contar(registro.RootElement);
    }

    private static int Contar(JsonElement no) => no.ValueKind switch
    {
        JsonValueKind.Object => no.EnumerateObject().Sum(static p => (p.Name == Chave ? 1 : 0) + Contar(p.Value)),
        JsonValueKind.Array => no.EnumerateArray().Sum(Contar),
        _ => 0,
    };
}

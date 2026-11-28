namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// O <c>PUT /processos-seletivos/{id}/cronograma-fases</c> com a instância superior do recurso e os integrantes e suplentes da
/// banca ausentes ou nulos: o domínio recusa com 422, o código genérico de campo obrigatório e um item por campo. A promessa de parecer individual da fase ausente ou nula é recusada do mesmo modo, em vez de valer "não promete".
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class CamposObrigatoriosDoCronogramaEndpointTests(CascadingFixture ambienteDeTeste)
{
    private const string RegraDeRecurso = "regraRecurso";
    private const string Deliberacao = "regraDeDeliberacao";

    [Theory(DisplayName = "A regra de recurso da fase sem a instância superior, omitida ou nula, é recusada pelo campo, sem recusar o prazo da 2ª instância declarado")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegraDeRecurso_SemInstanciaSuperior_Recusa(bool comNulo)
    {
        (Guid processoId, JsonArray fases) = await PrepararAsync();
        JsonObject regra = PayloadsDoEnvelopeFechado.RegraDeRecursoAncoradaNoResultado();
        Declarar(regra, "admiteInstanciaSuperior", comNulo);
        fases[1]![RegraDeRecurso] = regra;

        await ConferirRecusaAsync(processoId, fases, "fases[1].regraRecurso.admiteInstanciaSuperior");
    }

    [Theory(DisplayName = "A fase sem a promessa de parecer individual, omitida ou nula, é recusada pelo campo")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fase_SemPromessaDeParecer_Recusa(bool comNulo)
    {
        (Guid processoId, JsonArray fases) = await PrepararAsync();
        Declarar(fases[1]!.AsObject(), "emiteParecerIndividual", comNulo);

        await ConferirRecusaAsync(processoId, fases, "fases[1].emiteParecerIndividual");
    }

    [Theory(DisplayName = "A banca da fase sem integrantes e sem suplentes, omitidos ou nulos, é recusada nos dois campos, na ordem")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Banca_SemIntegrantesNemSuplentes_RecusaOsDoisCampos(bool comNulo)
    {
        (Guid processoId, JsonArray fases) = await PrepararAsync();
        JsonNode regra = fases[1]!["bancasRequeridas"]![0]![Deliberacao]!;
        Declarar(regra.AsObject(), "integrantes", comNulo);
        Declarar(regra.AsObject(), "suplentes", comNulo);

        await ConferirRecusaAsync(
            processoId,
            fases,
            "fases[1].bancasRequeridas[0].regraDeDeliberacao.integrantes",
            "fases[1].bancasRequeridas[0].regraDeDeliberacao.suplentes");
    }

    [Fact(DisplayName = "Zero suplentes declara nenhum e não é recusado como ausente")]
    public async Task Banca_ComZeroSuplentes_NaoRecusaPorAusencia()
    {
        (Guid processoId, JsonArray fases) = await PrepararAsync();
        fases[1]!["bancasRequeridas"]![0]![Deliberacao]!["suplentes"] = 0;

        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());

        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("uniplus.validacao.obrigatorio");
    }

    private static void Declarar(JsonObject objeto, string campo, bool comNulo)
    {
        if (comNulo)
        {
            objeto[campo] = null;
            return;
        }

        objeto.Remove(campo).Should().BeTrue();
    }

    private async Task ConferirRecusaAsync(Guid processoId, JsonArray fases, params string[] campos)
    {
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());

        await RecusaDeObrigatorioDoDominio.ConferirAsync(resposta, campos);
    }

    private async Task<(Guid ProcessoId, JsonArray Fases)> PrepararAsync()
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        Guid processoId = await SemearProcessoEmRascunhoAsync(
            ambienteDeTeste.Factory, nameof(CamposObrigatoriosDoCronogramaEndpointTests), OrigemCandidatos.InscricaoPropria);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado
            .SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray fases = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray();
        return (processoId, fases);
    }
}

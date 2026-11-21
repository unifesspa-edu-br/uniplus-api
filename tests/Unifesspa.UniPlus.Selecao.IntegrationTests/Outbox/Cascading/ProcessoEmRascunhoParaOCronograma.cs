namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// Um processo em rascunho, sem fase nenhuma, e as fases do catálogo que o teste vai pôr no cronograma: o que o teste do
/// <c>PUT</c> do cronograma com a marca precisa para gravar e para ler de volta pela API.
/// </summary>
internal sealed class ProcessoEmRascunhoParaOCronograma
{
    private readonly CascadingFixture _fixture;
    private readonly Dictionary<string, Guid> _fases;

    private ProcessoEmRascunhoParaOCronograma(CascadingFixture fixture, Guid processoId, Dictionary<string, Guid> fases)
    {
        _fixture = fixture;
        ProcessoId = processoId;
        _fases = fases;
    }

    internal Guid ProcessoId { get; }

    internal static async Task<ProcessoEmRascunhoParaOCronograma> PrepararAsync(CascadingFixture fixture, params string[] codigosDasFases)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(codigosDasFases);

        Dictionary<string, Guid> fases = [];
        foreach (string codigo in codigosDasFases)
        {
            fases[codigo] = await ObterFaseCanonicaSemeadaAsync(fixture.Factory, codigo);
        }

        Guid processoId = await SemearProcessoEmRascunhoAsync(fixture.Factory, "PS cronograma com a marca", OrigemCandidatos.ImportacaoExterna);

        return new ProcessoEmRascunhoParaOCronograma(fixture, processoId, fases);
    }

    /// <summary>
    /// Uma fase do corpo, pelo código no catálogo. O mês é o da ordem, a menos que o teste informe outro: ordens iguais com
    /// o mesmo mês se sobrepõem, e cada teste que quer a sobreposição a pede.
    /// </summary>
    internal JsonObject Fase(string codigo, int ordem, bool? marca = null, int? mes = null) =>
        CorpoDoCronogramaComMarca.Fase(_fases[codigo], ordem, mes ?? ordem, marca);

    internal async Task<HttpResponseMessage> GravarAsync(params JsonObject[] fases)
    {
        using HttpClient cliente = _fixture.Factory.CreateClient();

        return await EnviarCronogramaComCorpoCruAsync(cliente, ProcessoId, CorpoDoCronogramaComMarca.Corpo(fases));
    }

    /// <summary>A marca de cada fase como a leitura do processo a devolve, na ordem da leitura.</summary>
    internal async Task<bool[]> MarcasLidasAsync()
    {
        using HttpClient cliente = _fixture.Factory.CreateClient();

        return [.. (await LerProcessoPelaApiAsync(cliente, ProcessoId)).CronogramaFases.Select(static fase => fase.Cronograma.NoMesmoMomentoDaAnterior)];
    }
}

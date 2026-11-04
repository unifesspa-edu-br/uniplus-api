namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Leitura dos tipos de ato congelados.
/// </summary>
internal static class LeituraDosTiposDeAto
{
    /// <summary>
    /// Um tipo por código, com as três classificações sempre presentes: a ausência lida como falso
    /// congelaria "não é único", "não é resultado" ou "não congela" sem que o catálogo o tivesse dito. Todo
    /// tipo que o cronograma declara tem a sua entrada, porque é por ela que a publicação e a correção do
    /// item respondem.
    /// </summary>
    internal static Dictionary<string, TipoDeAtoCongelado> LerTiposDeAto(
        LeitorEnvelope leitor,
        JsonObject payload,
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<EtapaProcesso> etapas)
    {
        Dictionary<string, TipoDeAtoCongelado> tipos = new(StringComparer.Ordinal);
        if (leitor.Falhou)
        {
            return tipos;
        }

        JsonArray array = leitor.Array(payload, "tiposDeAto", "$");
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            TipoDeAtoCongelado? tipo = LerTipoDeAto(leitor, leitor.ItemObjeto(array, i, "tiposDeAto"), $"tiposDeAto[{i}]");
            if (tipo is null)
            {
                break;
            }

            if (!tipos.TryAdd(tipo.Codigo, tipo))
            {
                leitor.Recusar<object>($"Envelope malformado em 'tiposDeAto': o tipo '{tipo.Codigo}' aparece mais de uma vez.");
                return tipos;
            }
        }

        ConferirClassificacaoDosAtos(leitor, cronogramaFases, etapas, tipos);
        return tipos;
    }

    private static void ConferirClassificacaoDosAtos(
        LeitorEnvelope leitor,
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<EtapaProcesso> etapas,
        Dictionary<string, TipoDeAtoCongelado> tipos)
    {
        if (!leitor.Falhou && AtoDeclaradoSemClassificacao(cronogramaFases, etapas, tipos) is { } semClassificacao)
        {
            leitor.Recusar<object>(
                $"Envelope malformado em 'tiposDeAto': o tipo '{semClassificacao}', declarado no cronograma, não tem classificação congelada.");
        }
    }

    private static TipoDeAtoCongelado? LerTipoDeAto(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "codigo", "unicoPorObjeto", "ehResultado", "congelaConfiguracao");
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoAtoCodigo);
        bool unicoPorObjeto = leitor.Booleano(item, "unicoPorObjeto", path);
        bool ehResultado = leitor.Booleano(item, "ehResultado", path);
        bool congelaConfiguracao = leitor.Booleano(item, "congelaConfiguracao", path);
        return leitor.Falhou ? null : new TipoDeAtoCongelado(codigo, unicoPorObjeto, ehResultado, congelaConfiguracao);
    }

    private static string? AtoDeclaradoSemClassificacao(
        IReadOnlyList<FaseCronograma> cronogramaFases,
        IReadOnlyList<EtapaProcesso> etapas,
        Dictionary<string, TipoDeAtoCongelado> tipos) =>
        cronogramaFases.SelectMany(static fase => fase.Produtos.Select(static p => p.AtoCodigo))
            .Concat(etapas.SelectMany(static etapa => etapa.Produtos.Select(static p => p.AtoCodigo)))
            .FirstOrDefault(codigo => !tipos.ContainsKey(codigo));
}

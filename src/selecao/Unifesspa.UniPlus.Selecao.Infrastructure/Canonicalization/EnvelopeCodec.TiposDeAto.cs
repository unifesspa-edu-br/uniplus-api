namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Leitura do bloco <c>tiposDeAto</c>: a classificação, congelada na versão, de cada tipo de ato que o
/// cronograma declara.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// Um tipo por código, com as três classificações sempre presentes: a ausência lida como falso
    /// congelaria "não é único", "não é resultado" ou "não congela" sem que o catálogo o tivesse dito. Todo
    /// tipo que o cronograma declara tem a sua entrada, porque é por ela que a publicação e a correção do
    /// item respondem.
    /// </summary>
    private static Dictionary<string, TipoDeAtoCongelado> LerTiposDeAto(
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
            string path = $"tiposDeAto[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "tiposDeAto");
            leitor.ExigirChaves(item, path, "codigo", "unicoPorObjeto", "ehResultado", "congelaConfiguracao");
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoAtoCodigo);
            bool unicoPorObjeto = leitor.Booleano(item, "unicoPorObjeto", path);
            bool ehResultado = leitor.Booleano(item, "ehResultado", path);
            bool congelaConfiguracao = leitor.Booleano(item, "congelaConfiguracao", path);
            if (leitor.Falhou)
            {
                break;
            }

            if (!tipos.TryAdd(codigo, new TipoDeAtoCongelado(codigo, unicoPorObjeto, ehResultado, congelaConfiguracao)))
            {
                return leitor.Propagar<Dictionary<string, TipoDeAtoCongelado>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em 'tiposDeAto': o tipo '{codigo}' aparece mais de uma vez.")) ?? tipos;
            }
        }

        if (leitor.Falhou)
        {
            return tipos;
        }

        string? semClassificacao = cronogramaFases.SelectMany(static fase => fase.Produtos.Select(static p => p.AtoCodigo))
            .Concat(etapas.SelectMany(static etapa => etapa.Produtos.Select(static p => p.AtoCodigo)))
            .FirstOrDefault(codigo => !tipos.ContainsKey(codigo));

        return semClassificacao is null
            ? tipos
            : leitor.Propagar<Dictionary<string, TipoDeAtoCongelado>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'tiposDeAto': o tipo '{semClassificacao}', declarado no cronograma, não tem classificação congelada.")) ?? tipos;
    }
}

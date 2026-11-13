namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope o cronograma: fases, produtos, bancas requeridas e regras de recurso.
/// </summary>
internal static class SerializacaoDoCronograma
{
    internal static JsonObject SerializarCronogramaFases(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronograma(processo),
    };

    private static JsonArray SerializarFasesCronograma(ProcessoSeletivo processo) =>
        new([.. processo.CronogramaFases
            .OrderBy(static f => f.Ordem)
            .ThenBy(static f => f.Id)
            .Select(static fase => (JsonNode)SerializarFase(fase))]);

    private static JsonObject SerializarFase(FaseCronograma fase) => new()
    {
        ["id"] = fase.Id,
        ["ordem"] = fase.Ordem,
        ["faseCanonicaOrigemId"] = fase.FaseCanonicaOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(fase.Codigo),
        ["origemData"] = fase.OrigemData.ToString(),
        ["agrupaEtapas"] = fase.AgrupaEtapas,
        ["permiteComplementacao"] = fase.PermiteComplementacao,
        ["coletaInscricao"] = fase.ColetaInscricao,
        ["coletaSolicitacaoIsencao"] = fase.ColetaSolicitacaoIsencao,
        ["inicio"] = fase.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null,
        ["fim"] = fase.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null,
        ["produtos"] = SerializarProdutosDaFase(fase),
        ["faseConcluinteCodigo"] = fase.FaseConcluinteCodigo is { } concluinte
            ? HashCanonicalComputer.NormalizeNfc(concluinte)
            : null,
        ["emiteParecerIndividual"] = fase.EmiteParecerIndividual,
        ["bancasRequeridas"] = SerializarBancasRequeridas(fase),
        ["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFase(regraRecurso) : null,
        ["unidadeResponsavel"] = fase.UnidadeResponsavel is { } unidade ? SerializarUnidadeResponsavel(unidade) : null,
    };

    private static JsonObject SerializarUnidadeResponsavel(UnidadeResponsavelSnapshot unidade) => new()
    {
        ["origemId"] = unidade.OrigemId,
        ["sigla"] = HashCanonicalComputer.NormalizeNfc(unidade.Sigla),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(unidade.Nome),
    };

    private static JsonArray SerializarProdutosDaFase(FaseCronograma fase)
    {
        IOrderedEnumerable<ProdutoDaFase> ordenados = fase.Produtos
            .OrderBy(static p => p.AtoCodigo, StringComparer.Ordinal)
            .ThenBy(static p => p.Papel?.ToString() ?? string.Empty, StringComparer.Ordinal);

        return new JsonArray([.. ordenados.Select(static p => (JsonNode)new JsonObject
        {
            ["id"] = p.Id,
            ["atoCodigo"] = HashCanonicalComputer.NormalizeNfc(p.AtoCodigo),
            ["papel"] = p.Papel?.ToString(),
        })]);
    }

    private static JsonArray SerializarBancasRequeridas(FaseCronograma fase)
    {
        IOrderedEnumerable<(BancaRequerida Banca, JsonObject Item)> ordenadas = fase.BancasRequeridas
            .Select(static b => (Banca: b, Item: SerializarBancaRequerida(b)))
            .OrderBy(static par => par.Banca.TipoBancaOrigemId)
            .ThenBy(static par => par.Banca.Codigo, StringComparer.Ordinal)
            .ThenBy(
                static par => EscritaCanonica.PerfilAtual.Serializar(par.Item),
                ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenadas.Select(static par => (JsonNode)par.Item)]);
    }

    private static JsonObject SerializarBancaRequerida(BancaRequerida banca) => new()
    {
        ["tipoBancaOrigemId"] = banca.TipoBancaOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(banca.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(banca.Nome),
        ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(banca.RegraDeDeliberacao),
        ["recorteDeCompetencia"] = SerializarRecorteDeCompetencia(banca),
    };

    private static JsonArray SerializarRecorteDeCompetencia(BancaRequerida banca)
    {
        IOrderedEnumerable<CategoriaJulgada> ordenadas = banca.RecorteDeCompetencia
            .OrderBy(static c => c.Codigo, StringComparer.Ordinal);

        return new JsonArray([.. ordenadas.Select(static c => (JsonNode)new JsonObject
        {
            ["categoriaDocumentoOrigemId"] = c.CategoriaDocumentoOrigemId,
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(c.Codigo),
        })]);
    }

    private static JsonObject SerializarRegraRecursoFase(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = EscritaCanonica.SerializarReferenciaRegra(regraRecurso.Regra),
        ["produtoAncoraId"] = regraRecurso.ProdutoAncoraId,
        ["args"] = SerializarArgsRegraPrazoRecurso(regraRecurso.Args),
    };

    internal static JsonObject SerializarArgsRegraPrazoRecurso(ArgsRegraPrazoRecurso args) => new()
    {
        ["prazoValor"] = HashCanonicalComputer.SerializeDecimalCanonical(args.PrazoValor, EscritaCanonica.EscalaPadrao),
        ["prazoUnidade"] = args.PrazoUnidade.ToString(),
        ["suspensividadePrimeiraInstanciaValor"] = args.SuspensividadePrimeiraInstanciaValor is { } v1
            ? HashCanonicalComputer.SerializeDecimalCanonical(v1, EscritaCanonica.EscalaPadrao)
            : null,
        ["suspensividadePrimeiraInstanciaUnidade"] = args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
        ["admiteInstanciaSuperior"] = args.AdmiteInstanciaSuperior,
        ["suspensividadeSegundaInstanciaValor"] = args.SuspensividadeSegundaInstanciaValor is { } v2
            ? HashCanonicalComputer.SerializeDecimalCanonical(v2, EscritaCanonica.EscalaPadrao)
            : null,
        ["suspensividadeSegundaInstanciaUnidade"] = args.SuspensividadeSegundaInstanciaUnidade?.ToString(),
    };
}

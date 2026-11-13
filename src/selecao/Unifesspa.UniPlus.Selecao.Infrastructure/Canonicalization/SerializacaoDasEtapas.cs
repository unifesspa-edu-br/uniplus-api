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
/// Projeta no envelope as etapas do processo, ordenadas por posição e, no empate, pelo conteúdo canônico.
/// </summary>
internal static class SerializacaoDasEtapas
{
    internal static JsonArray SerializarEtapas(ProcessoSeletivo processo)
    {
        IOrderedEnumerable<(EtapaProcesso Etapa, JsonObject SemId)> ordenadas = processo.Etapas
            .Select(static e => (Etapa: e, SemId: SerializarEtapaSemIdentidade(e)))
            .OrderBy(static par => par.Etapa.Ordem ?? int.MaxValue)
            .ThenBy(
                static par => EscritaCanonica.PerfilAtual.Serializar(par.SemId),
                ComparadorLexicograficoDeBytes.Instancia)
            .ThenBy(static par => par.Etapa.Id);

        return new JsonArray([.. ordenadas.Select(static par =>
        {
            JsonObject item = par.SemId;
            item.Insert(0, "id", JsonValue.Create(par.Etapa.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarEtapaSemIdentidade(EtapaProcesso etapa) => new()
    {
        ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.Nome),
        ["carater"] = etapa.Carater.ToString(),
        ["tipoEtapa"] = SerializarTipoEtapa(etapa),
        ["peso"] = etapa.Peso is { } peso ? HashCanonicalComputer.SerializeDecimalCanonical(peso, EscritaCanonica.EscalaPadrao) : null,
        ["notaMinima"] = etapa.NotaMinima is { } notaMinima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMinima, EscritaCanonica.EscalaPadrao) : null,
        ["notaMaxima"] = etapa.NotaMaxima is { } notaMaxima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMaxima, EscritaCanonica.EscalaPadrao) : null,
        ["passoDoLancamento"] = etapa.PassoDoLancamento is { } passo ? HashCanonicalComputer.SerializeDecimalCanonical(passo, EscritaCanonica.EscalaPadrao) : null,
        ["formaDeAvaliacao"] = etapa.FormaDeAvaliacao?.ToString(),
        ["regraConsolidacao"] = etapa.RegraConsolidacao is { } consolidacao ? EscritaCanonica.SerializarReferenciaRegra(consolidacao) : null,
        ["itensDeAvaliacao"] = SerializarItensDeAvaliacao(etapa),
        ["escalaDeAtendimento"] = SerializarEscalaDeAtendimento(etapa),
        ["ordem"] = etapa.Ordem,
        ["faseCodigo"] = etapa.FaseCodigo,
        ["inicio"] = etapa.Inicio is { } ini ? HashCanonicalComputer.SerializeInstantCanonical(ini) : null,
        ["fim"] = etapa.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null,
        ["emiteParecerIndividual"] = etapa.EmiteParecerIndividual,
        ["bancas"] = SerializarBancas(etapa),
        ["recursos"] = SerializarRecursos(etapa),
        ["produtos"] = SerializarProdutos(etapa),
    };

    private static JsonArray SerializarItensDeAvaliacao(EtapaProcesso etapa) =>
        new([.. etapa.ItensDeAvaliacao.Select(static i => (JsonNode)new JsonObject
        {
            ["id"] = JsonValue.Create(i.Id),
            ["rotulo"] = HashCanonicalComputer.NormalizeNfc(i.Rotulo),
            ["pontuacaoMaxima"] = HashCanonicalComputer.SerializeDecimalCanonical(i.PontuacaoMaxima, EscritaCanonica.EscalaPadrao),
        })]);

    private static JsonArray? SerializarEscalaDeAtendimento(EtapaProcesso etapa) =>
        etapa.EscalaDeAtendimento is { } escala
            ? new JsonArray([.. escala.Select(static n => (JsonNode)new JsonObject
            {
                ["rotulo"] = HashCanonicalComputer.NormalizeNfc(n.Rotulo),
                ["fracao"] = HashCanonicalComputer.SerializeDecimalCanonical(n.Fracao, EscritaCanonica.EscalaPadrao),
            })])
            : null;

    private static JsonArray SerializarBancas(EtapaProcesso etapa) =>
        new([.. etapa.Bancas
            .OrderBy(static b => HashCanonicalComputer.NormalizeNfc(b.Codigo), StringComparer.Ordinal)
            .Select(static b => (JsonNode)new JsonObject
            {
                ["tipoBancaOrigemId"] = JsonValue.Create(b.TipoBancaOrigemId),
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(b.Codigo),
                ["nome"] = HashCanonicalComputer.NormalizeNfc(b.Nome),
                ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(b.RegraDeDeliberacao),
            })]);

    private static JsonArray SerializarRecursos(EtapaProcesso etapa) =>
        new([.. etapa.Recursos
            .OrderBy(static r => r.Ancora)
            .ThenBy(r => ChaveDoProdutoAncora(etapa, r.ProdutoAncoraId), StringComparer.Ordinal)
            .ThenBy(static r => HashCanonicalComputer.NormalizeNfc(r.Regra.Codigo), StringComparer.Ordinal)
            .ThenBy(static r => HashCanonicalComputer.NormalizeNfc(r.Regra.Versao), StringComparer.Ordinal)
            .ThenBy(static r => r.ProdutoAncoraId)
            .Select(static r => (JsonNode)new JsonObject
            {
                ["ancora"] = r.Ancora.ToString(),
                ["regra"] = EscritaCanonica.SerializarReferenciaRegra(r.Regra),
                ["args"] = SerializacaoDoCronograma.SerializarArgsRegraPrazoRecurso(r.Args),
                ["produtoAncoraId"] = r.ProdutoAncoraId == Guid.Empty ? null : JsonValue.Create(r.ProdutoAncoraId),
            })]);

    private static JsonArray SerializarProdutos(EtapaProcesso etapa) =>
        new([.. etapa.Produtos
            .OrderBy(static p => HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), StringComparer.Ordinal)
            .ThenBy(static p => p.Papel?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .Select(static p => (JsonNode)new JsonObject
            {
                ["id"] = JsonValue.Create(p.Id),
                ["atoCodigo"] = HashCanonicalComputer.NormalizeNfc(p.AtoCodigo),
                ["papel"] = p.Papel?.ToString(),
            })]);

    private static string ChaveDoProdutoAncora(EtapaProcesso etapa, Guid produtoAncoraId)
    {
        if (produtoAncoraId == Guid.Empty)
        {
            return string.Empty;
        }

        ProdutoDaEtapa? ancora = etapa.Produtos.FirstOrDefault(p => p.Id == produtoAncoraId);
        return ancora is null
            ? string.Empty
            : $"{HashCanonicalComputer.NormalizeNfc(ancora.AtoCodigo)}\u001f{ancora.Papel?.ToString() ?? string.Empty}";
    }

    private static JsonObject SerializarTipoEtapa(EtapaProcesso etapa) => new()
    {
        ["origemId"] = etapa.TipoEtapaOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Nome),
        ["admitePontuacao"] = etapa.TipoEtapa.AdmitePontuacao,
        ["admiteEliminacao"] = etapa.TipoEtapa.AdmiteEliminacao,
        ["notaDeOrigemNoEnem"] = etapa.TipoEtapa.NotaDeOrigemNoEnem,
    };
}

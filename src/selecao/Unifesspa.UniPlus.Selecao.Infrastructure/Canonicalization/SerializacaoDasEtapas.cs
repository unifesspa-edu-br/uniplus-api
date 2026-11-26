namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
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
            .OrderBy(static par => OrdemCanonicaDaConfiguracao.ChaveDaEtapa(par.Etapa))
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

    private static JsonObject SerializarEtapaSemIdentidade(EtapaProcesso etapa)
    {
        JsonObject item = new() { ["bancas"] = SerializarBancas(etapa) };
        AcrescentarIdentificacao(item, etapa);
        AcrescentarAvaliacao(item, etapa);
        AcrescentarJanela(item, etapa);
        AcrescentarPublicacoes(item, etapa);

        return item;
    }

    private static void AcrescentarIdentificacao(JsonObject item, EtapaProcesso etapa)
    {
        item["chave"] = etapa.Chave.Valor;
        item["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.Nome);
        item["carater"] = etapa.Carater.Codigo();
        ContextoDosRotulos.Acrescentar(item, "carater", etapa.Carater);
        item["tipoEtapa"] = SerializarTipoEtapa(etapa);
        item["ordem"] = etapa.Ordem;
        item["faseCodigo"] = etapa.FaseCodigo;
    }

    private static void AcrescentarAvaliacao(JsonObject item, EtapaProcesso etapa)
    {
        item["peso"] = SerializarDecimalOpcional(etapa.Peso);
        item["notaMinima"] = SerializarDecimalOpcional(etapa.NotaMinima);
        item["notaMaxima"] = SerializarDecimalOpcional(etapa.NotaMaxima);
        item["passoDoLancamento"] = SerializarDecimalOpcional(etapa.PassoDoLancamento);
        item["formaDeAvaliacao"] = etapa.FormaDeAvaliacao?.Codigo();
        ContextoDosRotulos.Acrescentar(item, "formaDeAvaliacao", etapa.FormaDeAvaliacao);
        item["regraDeApuracaoDaAvaliacao"] = etapa.RegraConsolidacao is { } consolidacao ? EscritaCanonica.SerializarReferenciaRegra(consolidacao) : null;
        item["itensDeAvaliacao"] = SerializarItensDeAvaliacao(etapa);
        item["escalaDeAtendimento"] = SerializarEscalaDeAtendimento(etapa);
    }

    private static void AcrescentarJanela(JsonObject item, EtapaProcesso etapa)
    {
        item["inicio"] = etapa.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null;
        item["fim"] = etapa.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null;
    }

    private static void AcrescentarPublicacoes(JsonObject item, EtapaProcesso etapa)
    {
        item["emiteParecerIndividual"] = etapa.EmiteParecerIndividual;
        item["recursos"] = SerializarRecursos(etapa);
        item["publicacoesPrevistas"] = SerializarProdutos(etapa);
        item["tiposDeDecisao"] = TiposDeDecisaoNoEnvelope.Serializar(etapa.TiposDeDecisao);
        TiposDeDecisaoNoEnvelope.AcrescentarContexto(item, "tiposDeDecisao", etapa.TiposDeDecisao);
    }

    private static string? SerializarDecimalOpcional(decimal? valor) =>
        valor is { } v ? HashCanonicalComputer.SerializeDecimalCanonical(v, EscritaCanonica.EscalaPadrao) : null;

    private static JsonArray SerializarItensDeAvaliacao(EtapaProcesso etapa) =>
        new([.. etapa.ItensDeAvaliacao.Select(static i => (JsonNode)new JsonObject
        {
            ["id"] = JsonValue.Create(i.Id),
            ["chave"] = i.Chave.Valor,
            ["ordem"] = i.Ordem,
            ["rotulo"] = HashCanonicalComputer.NormalizeNfc(i.Rotulo),
            ["pontuacaoMaxima"] = HashCanonicalComputer.SerializeDecimalCanonical(i.PontuacaoMaxima, EscritaCanonica.EscalaPadrao),
        })]);

    private static JsonArray? SerializarEscalaDeAtendimento(EtapaProcesso etapa) =>
        etapa.EscalaDeAtendimento is { } escala
            ? new JsonArray([.. escala.Select(static n => (JsonNode)new JsonObject
            {
                ["chave"] = n.Chave.Valor,
                ["ordem"] = n.Ordem,
                ["rotulo"] = HashCanonicalComputer.NormalizeNfc(n.Rotulo),
                ["fracao"] = HashCanonicalComputer.SerializeDecimalCanonical(n.Fracao, EscritaCanonica.EscalaPadrao),
            })])
            : null;

    private static JsonArray SerializarBancas(EtapaProcesso etapa) =>
        new([.. OrdemCanonicaDaConfiguracao.BancasDaEtapa(etapa.Bancas).Select(static b => (JsonNode)SerializarBanca(b))]);

    private static JsonObject SerializarBanca(BancaDaEtapa banca)
    {
        JsonObject item = new()
        {
            ["id"] = JsonValue.Create(banca.Id),
            ["chave"] = banca.Chave.Valor,
            ["posicaoNoTipo"] = JsonValue.Create(banca.PosicaoNoTipo),
            ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(banca.RegraDeDeliberacao),
        };
        item["consequenciaDaAusencia"] = banca.ConsequenciaDaAusencia?.Codigo();
        ContextoDosRotulos.Acrescentar(item, "consequenciaDaAusencia", banca.ConsequenciaDaAusencia);
        TipoDeBancaNoEnvelope.Serializar(item, banca.Tipo);

        return item;
    }

    private static JsonArray SerializarRecursos(EtapaProcesso etapa) =>
        new([.. etapa.Recursos
            .Select(static r => (JsonNode)SerializarRecurso(r))]);

    internal static JsonObject SerializarRecurso(RecursoDaEtapa recurso)
    {
        JsonObject objeto = new()
        {
            ["eventoInicialDoPrazo"] = recurso.Ancora.Codigo(),
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(recurso.Regra),
            ["parametros"] = SerializacaoDoCronograma.SerializarParametrosRegraPrazoRecurso(recurso.Parametros),
            ["publicacaoDeReferenciaId"] = recurso.ProdutoAncoraId == Guid.Empty ? null : JsonValue.Create(recurso.ProdutoAncoraId),
            ["procedimentos"] = SerializacaoDosProcedimentosDeRecurso.Serializar(recurso.Procedimentos),
        };
        ContextoDosRotulos.Acrescentar(objeto, "eventoInicialDoPrazo", recurso.Ancora);
        return objeto;
    }

    private static JsonArray SerializarProdutos(EtapaProcesso etapa) =>
        new([.. OrdemCanonicaDaConfiguracao.ProdutosDaEtapa(etapa.Produtos)
            .Select(static p => (JsonNode)SerializarProduto(p))]);

    private static JsonObject SerializarProduto(ProdutoDaEtapa produto)
    {
        JsonObject objeto = new()
        {
            ["id"] = JsonValue.Create(produto.Id),
            ["atoCodigo"] = HashCanonicalComputer.NormalizeNfc(produto.AtoCodigo),
            ["papel"] = produto.Papel?.Codigo(),
        };
        ContextoDosRotulos.Acrescentar(objeto, "papel", produto.Papel);
        return objeto;
    }

    private static JsonObject SerializarTipoEtapa(EtapaProcesso etapa) => new()
    {
        ["origemId"] = etapa.TipoEtapaOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.TipoEtapa.Nome),
        ["admitePontuacao"] = etapa.TipoEtapa.AdmitePontuacao,
        ["admiteEliminacao"] = etapa.TipoEtapa.AdmiteEliminacao,
        ["notaDeOrigemNoEnem"] = etapa.TipoEtapa.NotaDeOrigemNoEnem,
        ["exigeCorrecaoIndependente"] = etapa.TipoEtapa.ExigeCorrecaoIndependente,
    };
}

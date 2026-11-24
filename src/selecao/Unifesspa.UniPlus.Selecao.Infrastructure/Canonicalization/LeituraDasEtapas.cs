namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das etapas do processo, com itens de avaliação, escala de atendimento e tipo.
/// </summary>
internal static class LeituraDasEtapas
{
    private static readonly string[] ChavesDaEtapa =
    [
        "id", "chave", "nome", "carater", "tipoEtapa", "peso", "notaMinima", "notaMaxima", "passoDoLancamento", "formaDeAvaliacao",
        "regraDeApuracaoDaAvaliacao", "itensDeAvaliacao", "escalaDeAtendimento", "ordem", "faseCodigo", "publicacoesPrevistas",
        "inicio", "fim", "emiteParecerIndividual", "bancas", "recursos", "tiposDeDecisao",
    ];

    /// <summary>
    /// Só limites, e só limites: a coerência entre os itens, a nota máxima e o passo é regra de
    /// gravação e gate, e uma regra criada depois do congelamento não pode tornar ilegível um
    /// envelope que era válido quando nasceu.
    /// </summary>
    internal static List<ItemDeAvaliacao> LerItensDeAvaliacao(LeitorEnvelope leitor, JsonObject etapa, string caminho)
    {
        JsonArray array = leitor.Array(etapa, "itensDeAvaliacao", caminho);
        string caminhoItens = $"{caminho}.itensDeAvaliacao";
        List<(int Ordem, ItemDeAvaliacao Item)> ordenados = LerItensOrdenados(leitor, array, caminhoItens);
        List<ItemDeAvaliacao> itens = [.. ordenados.OrderBy(static par => par.Ordem).Select(static par => par.Item)];
        if (!leitor.Falhou)
        {
            ChavesNoEnvelope.ExigirUnicas(leitor, itens.Select(static item => item.Chave), caminhoItens);
        }

        return itens;
    }

    private static List<(int Ordem, ItemDeAvaliacao Item)> LerItensOrdenados(LeitorEnvelope leitor, JsonArray array, string caminho)
    {
        List<(int Ordem, ItemDeAvaliacao Item)> itens = [];
        for (int indice = 0; !leitor.Falhou && indice < array.Count; indice++)
        {
            JsonObject objeto = leitor.ItemObjeto(array, indice, caminho);
            string caminhoItem = $"{caminho}[{indice}]";
            ItemDeAvaliacao? item = LerItemDeAvaliacao(leitor, objeto, caminhoItem);
            int ordem = leitor.Inteiro(objeto, "ordem", caminhoItem);
            if (item is not null && !leitor.Falhou)
            {
                itens.Add((ordem, item));
            }
        }

        return itens;
    }

    private static ItemDeAvaliacao? LerItemDeAvaliacao(LeitorEnvelope leitor, JsonObject objeto, string caminhoItem)
    {
        leitor.ExigirChaves(objeto, caminhoItem, "id", "chave", "ordem", "rotulo", "pontuacaoMaxima");
        Guid id = leitor.Identificador(objeto, "id", caminhoItem);
        ChaveLocal chave = ChavesNoEnvelope.Ler(leitor, objeto, caminhoItem);
        string rotulo = leitor.TextoNaoVazio(objeto, "rotulo", caminhoItem, LimitesDoEnvelope.ItemDeAvaliacaoRotulo);
        decimal pontuacao = LerPontuacaoDoItem(leitor, objeto, caminhoItem);

        return leitor.Falhou ? null : ItemDeAvaliacao.Reidratar(chave, id, rotulo, pontuacao);
    }

    private static decimal LerPontuacaoDoItem(LeitorEnvelope leitor, JsonObject objeto, string caminhoItem)
    {
        decimal pontuacao = leitor.Decimal(objeto, "pontuacaoMaxima", EscritaCanonica.EscalaPadrao, caminhoItem, LimitesDoEnvelope.PrecisaoEtapa);

        return leitor.Falhou || pontuacao > 0
            ? pontuacao
            : leitor.Malformado<decimal>($"{caminhoItem}.pontuacaoMaxima", "a pontuação do item deve ser maior que zero.");
    }

    internal static List<NivelDeAtendimento>? LerEscalaDeAtendimento(LeitorEnvelope leitor, JsonObject etapa, string caminho)
    {
        JsonArray? array = leitor.ArrayOpcional(etapa, "escalaDeAtendimento", caminho);
        if (array is null)
        {
            return null;
        }

        string caminhoEscala = $"{caminho}.escalaDeAtendimento";
        List<NivelDeAtendimento> niveis = LerNiveisDeAtendimento(leitor, array, caminhoEscala);
        ExigirEscalaComNiveis(leitor, niveis, caminhoEscala);

        return niveis;
    }

    private static List<NivelDeAtendimento> LerNiveisDeAtendimento(LeitorEnvelope leitor, JsonArray array, string caminhoEscala)
    {
        List<(int Ordem, NivelDeAtendimento Nivel)> ordenados = LerNiveisOrdenados(leitor, array, caminhoEscala);
        List<NivelDeAtendimento> niveis = [.. ordenados.OrderBy(static par => par.Ordem).Select(static par => par.Nivel)];
        if (!leitor.Falhou)
        {
            ChavesNoEnvelope.ExigirUnicas(leitor, niveis.Select(static nivel => nivel.Chave), caminhoEscala);
        }

        return niveis;
    }

    private static List<(int Ordem, NivelDeAtendimento Nivel)> LerNiveisOrdenados(LeitorEnvelope leitor, JsonArray array, string caminho)
    {
        List<(int Ordem, NivelDeAtendimento Nivel)> niveis = [];
        for (int indice = 0; !leitor.Falhou && indice < array.Count; indice++)
        {
            JsonObject objeto = leitor.ItemObjeto(array, indice, caminho);
            string caminhoNivel = $"{caminho}[{indice}]";
            NivelDeAtendimento? nivel = LerNivelDeAtendimento(leitor, objeto, caminhoNivel);
            int ordem = leitor.Inteiro(objeto, "ordem", caminhoNivel);
            if (nivel is not null && !leitor.Falhou)
            {
                niveis.Add((ordem, nivel));
            }
        }

        return niveis;
    }

    private static void ExigirEscalaComNiveis(LeitorEnvelope leitor, List<NivelDeAtendimento> niveis, string caminhoEscala)
    {
        if (!leitor.Falhou && niveis.Count == 0)
        {
            leitor.Malformado<object>(caminhoEscala, "a escala sem níveis deve ser null.");
        }
    }

    private static NivelDeAtendimento? LerNivelDeAtendimento(LeitorEnvelope leitor, JsonObject objeto, string caminhoNivel)
    {
        leitor.ExigirChaves(objeto, caminhoNivel, "chave", "ordem", "rotulo", "fracao");
        ChaveLocal chave = ChavesNoEnvelope.Ler(leitor, objeto, caminhoNivel);
        string rotulo = leitor.TextoNaoVazio(objeto, "rotulo", caminhoNivel, LimitesDoEnvelope.NivelDeAtendimentoRotulo);
        decimal fracao = leitor.Decimal(objeto, "fracao", EscritaCanonica.EscalaPadrao, caminhoNivel, LimitesDoEnvelope.PrecisaoFracaoDeAtendimento);
        if (leitor.Falhou)
        {
            return null;
        }

        return fracao is < 0m or > 1m
            ? leitor.Malformado<NivelDeAtendimento?>($"{caminhoNivel}.fracao", "a fração do nível deve estar entre 0 e 1.")
            : NivelDeAtendimento.Reidratar(chave, rotulo, fracao);
    }

    internal static IReadOnlyList<EtapaProcesso> LerEtapas(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "etapas", "$");

        return leitor.Falhou ? [] : LerEtapasDoArray(leitor, array);
    }

    private static List<EtapaProcesso> LerEtapasDoArray(LeitorEnvelope leitor, JsonArray array)
    {
        List<EtapaProcesso> etapas = [];
        for (int i = 0; i < array.Count; i++)
        {
            EtapaProcesso? etapa = LerEtapa(leitor, leitor.ItemObjeto(array, i, "etapas"), $"etapas[{i}]");
            if (etapa is null)
            {
                return [];
            }

            etapas.Add(etapa);
        }

        ChavesNoEnvelope.ExigirUnicas(leitor, etapas.Select(static etapa => etapa.Chave), "etapas");
        return leitor.Falhou ? [] : etapas;
    }

    private static EtapaProcesso? LerEtapa(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        leitor.ExigirChaves(item, caminho, ChavesDaEtapa);

        EtapaProcesso? etapa = ReidratarEtapa(leitor, item, caminho);
        if (etapa is null)
        {
            return null;
        }

        List<ProdutoDaEtapa> produtos = LerProdutosDaEtapa(leitor, item, caminho);

        bool propria = DefinirJanelaEParecer(leitor, etapa, item, caminho) is not null
            && DeclararTiposDeDecisao(leitor, etapa, item, caminho) is not null;

        return propria ? DeclararFilhas(leitor, etapa, produtos, item, caminho) : null;
    }

    private static EtapaProcesso? DeclararTiposDeDecisao(LeitorEnvelope leitor, EtapaProcesso etapa, JsonObject item, string caminho)
    {
        IReadOnlyList<string> tipos = TiposDeDecisaoNoEnvelope.Ler(leitor, item, caminho);
        if (leitor.Falhou)
        {
            return null;
        }

        Result declaracao = etapa.DefinirTiposDeDecisao(tipos);

        return declaracao.IsFailure ? leitor.Propagar<EtapaProcesso?>(declaracao.Error!) : etapa;
    }

    private static EtapaProcesso? DeclararFilhas(
        LeitorEnvelope leitor, EtapaProcesso etapa, List<ProdutoDaEtapa> produtos, JsonObject item, string caminho)
    {
        return DefinirBancas(leitor, etapa, LerBancasDaEtapa(leitor, item, caminho)) is not null
            && DefinirProdutos(leitor, etapa, produtos, caminho) is not null
            ? DefinirRecursos(leitor, etapa, item, caminho)
            : null;
    }

    // As bancas chegam à etapa, que lhes atribui a posição no tipo; a lida tem de ser essa.
    private static EtapaProcesso? DefinirBancas(LeitorEnvelope leitor, EtapaProcesso etapa, BancasLidas<BancaDaEtapa> lidas)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        etapa.DefinirBancas(lidas.Bancas);
        lidas.ExigirQueSeguemARaiz(leitor, etapa.Bancas.ToDictionary(static banca => banca.Id, static banca => banca.PosicaoNoTipo));

        return leitor.Falhou ? null : etapa;
    }

    private static EtapaProcesso? DefinirProdutos(LeitorEnvelope leitor, EtapaProcesso etapa, List<ProdutoDaEtapa> produtos, string caminho) =>
        etapa.DefinirProdutos(produtos).IsFailure
            ? RecusarEtapa(leitor, $"{caminho}.publicacoesPrevistas", "o mesmo ato aparece duas vezes na etapa.")
            : etapa;

    private static EtapaProcesso? DefinirJanelaEParecer(LeitorEnvelope leitor, EtapaProcesso etapa, JsonObject item, string caminho)
    {
        DateTimeOffset? inicio = leitor.InstanteOpcional(item, "inicio", caminho);
        DateTimeOffset? fim = leitor.InstanteOpcional(item, "fim", caminho);
        bool parecer = leitor.Booleano(item, "emiteParecerIndividual", caminho);

        if (leitor.Falhou)
        {
            return null;
        }

        return etapa.DefinirJanelaEParecer(inicio, fim, parecer).IsFailure
            ? RecusarEtapa(leitor, caminho, "a janela da etapa está invertida.")
            : etapa;
    }

    private static EtapaProcesso? DefinirRecursos(LeitorEnvelope leitor, EtapaProcesso etapa, JsonObject item, string caminho)
    {
        List<RecursoDaEtapa>? recursos = LeituraDosRecursosDaEtapa.Ler(leitor, item, caminho);
        if (recursos is null)
        {
            return null;
        }

        return etapa.DefinirRecursos(recursos).IsFailure
            ? RecusarEtapa(leitor, $"{caminho}.recursos", "a âncora não resolve na etapa.")
            : etapa;
    }

    private static EtapaProcesso? RecusarEtapa(LeitorEnvelope leitor, string caminho, string detalhe) =>
        leitor.Malformado<EtapaProcesso?>(caminho, detalhe);

    private static EtapaProcesso? ReidratarEtapa(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        Guid id = leitor.Identificador(item, "id", caminho);
        ChaveLocal chave = ChavesNoEnvelope.Ler(leitor, item, caminho);
        string nome = leitor.TextoNaoVazio(item, "nome", caminho, LimitesDoEnvelope.EtapaNome);
        CaraterEtapa carater = leitor.CodigoDeContrato<CaraterEtapa>(item, "carater", caminho);
        TipoEtapaSnapshot? tipoEtapa = LeituraDoTipoDaEtapa.Ler(leitor, item, caminho);
        (DeclaracaoDaAvaliacaoDaEtapa avaliacao, List<ItemDeAvaliacao> itens) = LerAvaliacaoDaEtapa(leitor, item, caminho);
        (int ordem, string? faseCodigo) = (
            leitor.Inteiro(item, "ordem", caminho), leitor.TextoOpcional(item, "faseCodigo", caminho, LimitesDoEnvelope.FaseCodigo));

        return leitor.Falhou
            ? null
            : ReidratarDentroDosLimites(leitor, caminho, (id, chave), new IdentificacaoDaEtapa(nome, carater, tipoEtapa!, ordem, faseCodigo), avaliacao, itens);
    }

    private static (DeclaracaoDaAvaliacaoDaEtapa Avaliacao, List<ItemDeAvaliacao> Itens) LerAvaliacaoDaEtapa(
        LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        decimal? peso = LerDecimalDaEtapa(leitor, item, "peso", caminho);
        decimal? notaMinima = LerDecimalDaEtapa(leitor, item, "notaMinima", caminho);
        decimal? notaMaxima = LerDecimalDaEtapa(leitor, item, "notaMaxima", caminho);
        decimal? passoDoLancamento = LerDecimalDaEtapa(leitor, item, "passoDoLancamento", caminho);
        FormaDeAvaliacao? formaDeAvaliacao = leitor.CodigoDeContratoOpcional<FormaDeAvaliacao>(item, "formaDeAvaliacao", caminho);
        ReferenciaRegra? regraConsolidacao = leitor.RegraOpcional<RegraDeApuracaoDaAvaliacao>(item, "regraDeApuracaoDaAvaliacao", caminho);
        List<ItemDeAvaliacao> itens = LerItensDeAvaliacao(leitor, item, caminho);
        List<NivelDeAtendimento>? escalaDeAtendimento = LerEscalaDeAtendimento(leitor, item, caminho);

        return (new(peso, notaMinima, notaMaxima, passoDoLancamento, formaDeAvaliacao, regraConsolidacao, escalaDeAtendimento), itens);
    }

    // Limites que hoje só o FluentValidation do comando impõe: um envelope legítimo já os
    // satisfaz. Regra de coerência entre campos fica de fora de propósito, porque uma regra criada
    // depois do congelamento tornaria ilegível um envelope que era válido quando nasceu.
    private static EtapaProcesso? ReidratarDentroDosLimites(
        LeitorEnvelope leitor,
        string caminho,
        (Guid Id, ChaveLocal Chave) identidade,
        IdentificacaoDaEtapa identificacao,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        List<ItemDeAvaliacao> itens) =>
        ForaDosLimites(identificacao, avaliacao)
            ? RecusarEtapa(leitor, caminho, "peso, ordem, nota máxima e passo do lançamento devem ser maiores que zero e a nota mínima não negativa.")
            : EtapaProcesso.Reidratar(identidade.Chave, identidade.Id, identificacao, avaliacao, itens);

    private static bool ForaDosLimites(IdentificacaoDaEtapa identificacao, DeclaracaoDaAvaliacaoDaEtapa avaliacao) =>
        avaliacao.Peso is <= 0
        || avaliacao.NotaMinima is < 0
        || avaliacao.NotaMaxima is <= 0
        || avaliacao.PassoDoLancamento is <= 0
        || identificacao.Ordem is <= 0;

    private static decimal? LerDecimalDaEtapa(LeitorEnvelope leitor, JsonObject item, string chave, string caminho) =>
        leitor.DecimalOpcional(item, chave, EscritaCanonica.EscalaPadrao, caminho, LimitesDoEnvelope.PrecisaoEtapa);

    private static List<ProdutoDaEtapa> LerProdutosDaEtapa(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        JsonArray? array = leitor.Array(item, "publicacoesPrevistas", caminho);

        return array is null ? [] : LerProdutosDoArray(leitor, array, $"{caminho}.publicacoesPrevistas");
    }

    private static List<ProdutoDaEtapa> LerProdutosDoArray(LeitorEnvelope leitor, JsonArray array, string caminhoProdutos)
    {
        List<ProdutoDaEtapa> produtos = [];
        for (int j = 0; j < array.Count; j++)
        {
            // O caminho que ItemObjeto recebe é o do array, porque é ele que acrescenta o índice.
            if (LerProdutoDaEtapa(leitor, leitor.ItemObjeto(array, j, caminhoProdutos), $"{caminhoProdutos}[{j}]") is not { } produto)
            {
                return [];
            }

            produtos.Add(produto);
        }

        return produtos;
    }

    private static ProdutoDaEtapa? LerProdutoDaEtapa(LeitorEnvelope leitor, JsonObject itemProduto, string caminhoProduto)
    {
        leitor.ExigirChaves(itemProduto, caminhoProduto, "id", "atoCodigo", "papel");
        Guid id = leitor.Identificador(itemProduto, "id", caminhoProduto);
        string ato = leitor.TextoNaoVazio(itemProduto, "atoCodigo", caminhoProduto, LimitesDoEnvelope.TipoAtoCodigo);
        PapelDaPublicacaoPrevista? papel = leitor.CodigoDeContratoOpcional<PapelDaPublicacaoPrevista>(itemProduto, "papel", caminhoProduto);

        return leitor.Falhou ? null : ProdutoDaEtapa.Reidratar(id, ato, papel);
    }

    private static BancasLidas<BancaDaEtapa> LerBancasDaEtapa(LeitorEnvelope leitor, JsonObject item, string caminho) =>
        BancasLidas<BancaDaEtapa>.Ler(leitor, leitor.Array(item, "bancas", caminho), $"{caminho}.bancas", LerBancaDaEtapa);

    private static (BancaDaEtapa, BancaLida)? LerBancaDaEtapa(LeitorEnvelope leitor, JsonObject itemBanca, string caminhoBanca)
    {
        leitor.ExigirChaves(itemBanca, caminhoBanca, [.. TipoDeBancaNoEnvelope.Chaves, "id", "chave", "posicaoNoTipo", "regraDeDeliberacao"]);
        BancaLida? lida = BancaLida.Ler(leitor, itemBanca, caminhoBanca);

        return lida is null
            ? null
            : (BancaDaEtapa.ReconstruirDosDadosSalvos(lida.Chave, lida.Id, lida.Tipo, lida.Regra), lida);
    }
}

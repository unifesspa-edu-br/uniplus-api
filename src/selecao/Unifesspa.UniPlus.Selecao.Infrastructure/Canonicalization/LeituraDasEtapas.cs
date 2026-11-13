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
        "id", "nome", "carater", "tipoEtapa", "peso", "notaMinima", "notaMaxima", "passoDoLancamento", "formaDeAvaliacao",
        "regraConsolidacao", "itensDeAvaliacao", "escalaDeAtendimento", "ordem", "faseCodigo", "produtos",
        "inicio", "fim", "emiteParecerIndividual", "bancas", "recursos",
    ];

    /// <summary>
    /// Só limites, e só limites: a coerência entre os itens, a nota máxima e o passo é regra de
    /// gravação e gate, e uma regra criada depois do congelamento não pode tornar ilegível um
    /// envelope que era válido quando nasceu.
    /// </summary>
    internal static List<ItemDeAvaliacao> LerItensDeAvaliacao(LeitorEnvelope leitor, JsonObject etapa, string path)
    {
        JsonArray array = leitor.Array(etapa, "itensDeAvaliacao", path);
        string pathItens = $"{path}.itensDeAvaliacao";
        List<ItemDeAvaliacao> itens = [];
        for (int j = 0; !leitor.Falhou && j < array.Count; j++)
        {
            string pathItem = $"{pathItens}[{j}]";
            JsonObject objeto = leitor.ItemObjeto(array, j, pathItens);
            leitor.ExigirChaves(objeto, pathItem, "id", "rotulo", "pontuacaoMaxima");
            Guid id = leitor.Identificador(objeto, "id", pathItem);
            string rotulo = leitor.TextoNaoVazio(objeto, "rotulo", pathItem, LimitesDoEnvelope.ItemDeAvaliacaoRotulo);
            decimal pontuacao = leitor.Decimal(objeto, "pontuacaoMaxima", EscritaCanonica.EscalaPadrao, pathItem, LimitesDoEnvelope.PrecisaoEtapa);
            if (leitor.Falhou)
            {
                break;
            }

            if (pontuacao <= 0)
            {
                leitor.Propagar<IReadOnlyList<ItemDeAvaliacao>>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"Envelope malformado em '{pathItem}.pontuacaoMaxima': a pontuação do item deve ser maior que zero."));
                break;
            }

            itens.Add(ItemDeAvaliacao.Reidratar(id, rotulo, pontuacao));
        }

        return itens;
    }

    internal static List<NivelDeAtendimento>? LerEscalaDeAtendimento(LeitorEnvelope leitor, JsonObject etapa, string path)
    {
        JsonArray? array = leitor.ArrayOpcional(etapa, "escalaDeAtendimento", path);
        if (array is null)
        {
            return null;
        }

        string pathEscala = $"{path}.escalaDeAtendimento";
        List<NivelDeAtendimento> niveis = [];
        for (int j = 0; !leitor.Falhou && j < array.Count; j++)
        {
            if (LerNivelDeAtendimento(leitor, leitor.ItemObjeto(array, j, pathEscala), $"{pathEscala}[{j}]") is not { } nivel)
            {
                break;
            }

            niveis.Add(nivel);
        }

        if (!leitor.Falhou && niveis.Count == 0)
        {
            leitor.Malformado<object>(pathEscala, "a escala sem níveis deve ser null.");
        }

        return niveis;
    }

    private static NivelDeAtendimento? LerNivelDeAtendimento(LeitorEnvelope leitor, JsonObject objeto, string pathNivel)
    {
        leitor.ExigirChaves(objeto, pathNivel, "rotulo", "fracao");
        string rotulo = leitor.TextoNaoVazio(objeto, "rotulo", pathNivel, LimitesDoEnvelope.NivelDeAtendimentoRotulo);
        decimal fracao = leitor.Decimal(objeto, "fracao", EscritaCanonica.EscalaPadrao, pathNivel, LimitesDoEnvelope.PrecisaoFracaoDeAtendimento);
        if (leitor.Falhou)
        {
            return null;
        }

        if (fracao is < 0m or > 1m)
        {
            return leitor.Malformado<NivelDeAtendimento?>($"{pathNivel}.fracao", "a fração do nível deve estar entre 0 e 1.");
        }

        return new NivelDeAtendimento(rotulo, fracao);
    }

    internal static IReadOnlyList<EtapaProcesso> LerEtapas(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "etapas", "$");
        if (leitor.Falhou)
        {
            return [];
        }

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

        return etapas;
    }

    private static EtapaProcesso? LerEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaEtapa);

        EtapaProcesso? etapa = ReidratarEtapa(leitor, item, path);
        if (etapa is null)
        {
            return null;
        }

        List<ProdutoDaEtapa> produtos = LerProdutosDaEtapa(leitor, item, path);
        if (DefinirJanelaEParecer(leitor, etapa, item, path) is null)
        {
            return null;
        }

        List<BancaDaEtapa> bancas = LerBancasDaEtapa(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (etapa.DefinirBancas(bancas).IsFailure)
        {
            return RecusarEtapa(leitor, $"{path}.bancas", "o mesmo tipo aparece duas vezes.");
        }

        if (etapa.DefinirProdutos(produtos).IsFailure)
        {
            return RecusarEtapa(leitor, $"{path}.produtos", "o mesmo ato aparece duas vezes na etapa.");
        }

        return DefinirRecursos(leitor, etapa, item, path);
    }

    private static EtapaProcesso? DefinirJanelaEParecer(LeitorEnvelope leitor, EtapaProcesso etapa, JsonObject item, string path)
    {
        DateTimeOffset? inicio = leitor.InstanteOpcional(item, "inicio", path);
        DateTimeOffset? fim = leitor.InstanteOpcional(item, "fim", path);
        bool parecer = leitor.Booleano(item, "emiteParecerIndividual", path);

        if (leitor.Falhou)
        {
            return null;
        }

        return etapa.DefinirJanelaEParecer(inicio, fim, parecer).IsFailure
            ? RecusarEtapa(leitor, path, "a janela da etapa está invertida.")
            : etapa;
    }

    private static EtapaProcesso? DefinirRecursos(LeitorEnvelope leitor, EtapaProcesso etapa, JsonObject item, string path)
    {
        List<RecursoDaEtapa>? recursos = LerRecursosDaEtapa(leitor, item, path);
        if (recursos is null)
        {
            return null;
        }

        return etapa.DefinirRecursos(recursos).IsFailure
            ? RecusarEtapa(leitor, $"{path}.recursos", "a âncora não resolve na etapa.")
            : etapa;
    }

    private static EtapaProcesso? RecusarEtapa(LeitorEnvelope leitor, string path, string detalhe) =>
        leitor.Malformado<EtapaProcesso?>(path, detalhe);

    private static EtapaProcesso? ReidratarEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        Guid id = leitor.Identificador(item, "id", path);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.EtapaNome);
        CaraterEtapa carater = leitor.Enumeracao<CaraterEtapa>(item, "carater", path);
        TipoEtapaSnapshot? tipoEtapa = LerTipoEtapaDaEtapa(leitor, item, path);
        decimal? peso = LerDecimalDaEtapa(leitor, item, "peso", path);
        decimal? notaMinima = LerDecimalDaEtapa(leitor, item, "notaMinima", path);
        decimal? notaMaxima = LerDecimalDaEtapa(leitor, item, "notaMaxima", path);
        decimal? passoDoLancamento = LerDecimalDaEtapa(leitor, item, "passoDoLancamento", path);
        FormaDeAvaliacao? formaDeAvaliacao = leitor.EnumeracaoOpcional<FormaDeAvaliacao>(item, "formaDeAvaliacao", path);
        ReferenciaRegra? regraConsolidacao = leitor.RegraOpcional(item, "regraConsolidacao", path, [.. RegraConsolidacaoCodigo.Rol]);
        List<ItemDeAvaliacao> itensDeAvaliacao = LerItensDeAvaliacao(leitor, item, path);
        List<NivelDeAtendimento>? escalaDeAtendimento = LerEscalaDeAtendimento(leitor, item, path);
        int? ordem = leitor.InteiroOpcional(item, "ordem", path);
        string? faseCodigo = leitor.TextoOpcional(item, "faseCodigo", path, LimitesDoEnvelope.FaseCodigo);

        if (leitor.Falhou)
        {
            return null;
        }

        // Limites que hoje só o FluentValidation do comando impõe: um envelope legítimo já os
        // satisfaz. Regra de coerência entre campos fica de fora de propósito, porque uma regra criada
        // depois do congelamento tornaria ilegível um envelope que era válido quando nasceu.
        return ForaDosLimites(carater, peso, notaMinima, notaMaxima, passoDoLancamento, ordem)
            ? RecusarEtapa(leitor, path, "peso, ordem, nota máxima e passo do lançamento devem ser maiores que zero e a nota mínima não negativa.")
            : EtapaProcesso.Reidratar(id, nome, carater, tipoEtapa!, peso, notaMinima, ordem, faseCodigo, notaMaxima, passoDoLancamento, formaDeAvaliacao, regraConsolidacao, itensDeAvaliacao, escalaDeAtendimento);
    }

    private static bool ForaDosLimites(CaraterEtapa carater, decimal? peso, decimal? notaMinima, decimal? notaMaxima, decimal? passoDoLancamento, int? ordem) =>
        carater == CaraterEtapa.Nenhum
        || peso is <= 0
        || notaMinima is < 0
        || notaMaxima is <= 0
        || passoDoLancamento is <= 0
        || ordem is <= 0;

    private static decimal? LerDecimalDaEtapa(LeitorEnvelope leitor, JsonObject item, string chave, string path) =>
        leitor.DecimalOpcional(item, chave, EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoEtapa);

    private static List<ProdutoDaEtapa> LerProdutosDaEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonArray? array = leitor.Array(item, "produtos", path);
        string pathProdutos = $"{path}.produtos";
        List<ProdutoDaEtapa> produtos = [];
        for (int j = 0; array is not null && j < array.Count; j++)
        {
            string pathProduto = $"{pathProdutos}[{j}]";

            // O caminho que ItemObjeto recebe é o do array, porque é ele que acrescenta o índice.
            JsonObject itemProduto = leitor.ItemObjeto(array, j, pathProdutos);
            leitor.ExigirChaves(itemProduto, pathProduto, "id", "atoCodigo", "papel");
            Guid idProduto = leitor.Identificador(itemProduto, "id", pathProduto);
            string ato = leitor.TextoNaoVazio(itemProduto, "atoCodigo", pathProduto, LimitesDoEnvelope.TipoAtoCodigo);
            PapelProdutoFase? papel = leitor.EnumeracaoOpcional<PapelProdutoFase>(itemProduto, "papel", pathProduto);
            if (leitor.Falhou)
            {
                return [];
            }

            produtos.Add(ProdutoDaEtapa.Reidratar(idProduto, ato, papel));
        }

        return produtos;
    }

    private static List<BancaDaEtapa> LerBancasDaEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonArray? array = leitor.Array(item, "bancas", path);
        string pathBancas = $"{path}.bancas";
        List<BancaDaEtapa> bancas = [];
        for (int j = 0; !leitor.Falhou && array is not null && j < array.Count; j++)
        {
            BancaDaEtapa? banca = LerBancaDaEtapa(leitor, leitor.ItemObjeto(array, j, pathBancas), $"{pathBancas}[{j}]");
            if (banca is not null)
            {
                bancas.Add(banca);
            }
        }

        return leitor.Falhou ? [] : bancas;
    }

    // Id novo: o envelope não congela a identidade da linha porque nada a referencia.
    private static BancaDaEtapa? LerBancaDaEtapa(LeitorEnvelope leitor, JsonObject itemBanca, string pathBanca)
    {
        leitor.ExigirChaves(itemBanca, pathBanca, "tipoBancaOrigemId", "codigo", "nome", "regraDeDeliberacao");
        BancaLida? lida = BancaLida.Ler(leitor, itemBanca, pathBanca);

        return lida is null ? null : BancaDaEtapa.Criar(lida.TipoBancaOrigemId, lida.Codigo, lida.Nome, lida.Regra);
    }

    private static List<RecursoDaEtapa>? LerRecursosDaEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonArray? array = leitor.Array(item, "recursos", path);
        string pathRecursos = $"{path}.recursos";
        List<RecursoDaEtapa> recursos = [];
        for (int j = 0; array is not null && j < array.Count; j++)
        {
            RecursoDaEtapa? recurso = LerRecursoDaEtapa(leitor, leitor.ItemObjeto(array, j, pathRecursos), $"{pathRecursos}[{j}]");
            if (recurso is null)
            {
                return null;
            }

            recursos.Add(recurso);
        }

        return recursos;
    }

    private static RecursoDaEtapa? LerRecursoDaEtapa(LeitorEnvelope leitor, JsonObject itemRecurso, string pathRecurso)
    {
        leitor.ExigirChaves(itemRecurso, pathRecurso, "ancora", "regra", "args", "produtoAncoraId");
        AncoraDoRecurso ancora = leitor.Enumeracao<AncoraDoRecurso>(itemRecurso, "ancora", pathRecurso);

        // A referência vem inteira e conferida contra o código de regra esperado.
        ReferenciaRegra regra = leitor.Regra(itemRecurso, "regra", pathRecurso, RegraPrazoRecursoCodigo.AncoradoEmAto);

        // A âncora por ciência não tem produto: o campo vem nulo.
        Guid? ancoraId = leitor.IdentificadorOpcional(itemRecurso, "produtoAncoraId", pathRecurso);
        JsonObject argsObjeto = leitor.Objeto(itemRecurso, "args", pathRecurso);
        if (leitor.Falhou)
        {
            return null;
        }

        // Nenhuma é o sentinela de ausência que RecursoDaEtapa.Criar recusa e que Reidratar não reconfere.
        if (ancora == AncoraDoRecurso.Nenhuma)
        {
            return leitor.Malformado<RecursoDaEtapa?>($"{pathRecurso}.ancora", "o recurso tem de declarar de que instante o prazo corre.");
        }

        ArgsRegraPrazoRecurso? args = LeituraDosPrazosDeRecurso.LerArgsDePrazoDeRecurso(leitor, argsObjeto, $"{pathRecurso}.args");
        if (leitor.Falhou || args is null)
        {
            return null;
        }

        // Identidade nova: o que a publicação promete é a janela, não a linha que a guarda.
        Result<RecursoDaEtapa> recurso = RecursoDaEtapa.Reidratar(Guid.CreateVersion7(), ancora, regra, args, ancoraId ?? Guid.Empty);
        return recurso.IsFailure
            ? leitor.Propagar<RecursoDaEtapa?>(recurso.Error!)
            : recurso.Value;
    }

    /// <summary>
    /// Lê o snapshot de tipo aninhado em cada item de <c>etapas</c> — mesmo
    /// shape do bloco de topo <c>tipoProcesso</c>, mas diferente daquele (só forma, nunca
    /// reidratado: o tipo do processo não muda ao restaurar uma versão), o valor lido aqui é
    /// efetivamente usado para reconstruir a identidade de tipo da etapa. Um envelope
    /// adulterado com <c>origemId</c>/<c>codigo</c>/<c>nome</c> vazio ou acima do limite é
    /// recusado como malformado pela própria factory do VO, nunca reidratado.
    /// </summary>
    internal static TipoEtapaSnapshot? LerTipoEtapaDaEtapa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonObject tipo = leitor.Objeto(item, "tipoEtapa", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string tipoPath = $"{path}.tipoEtapa";
        leitor.ExigirChaves(
            tipo, tipoPath, "origemId", "codigo", "nome", "admitePontuacao", "admiteEliminacao", "notaDeOrigemNoEnem");
        Guid origemId = leitor.Identificador(tipo, "origemId", tipoPath);
        string codigo = leitor.TextoNaoVazio(tipo, "codigo", tipoPath, LimitesDoEnvelope.TipoEtapaCodigo);
        string nome = leitor.TextoNaoVazio(tipo, "nome", tipoPath, LimitesDoEnvelope.TipoEtapaNome);
        bool admitePontuacao = leitor.Booleano(tipo, "admitePontuacao", tipoPath);
        bool admiteEliminacao = leitor.Booleano(tipo, "admiteEliminacao", tipoPath);
        bool notaDeOrigemNoEnem = leitor.Booleano(tipo, "notaDeOrigemNoEnem", tipoPath);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(
            origemId, codigo, nome, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem);
        return resultado.IsFailure ? leitor.Propagar<TipoEtapaSnapshot>(resultado.Error!) : resultado.Value;
    }
}

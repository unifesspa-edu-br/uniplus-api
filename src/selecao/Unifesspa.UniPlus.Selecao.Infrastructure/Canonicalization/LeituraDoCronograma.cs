namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das fases do cronograma.
/// </summary>
internal static class LeituraDoCronograma
{
    private static readonly string[] ChavesDaFase =
    [
        "ordem", "faseCanonicaOrigemId", "codigo", "origemData", "agrupaEtapas", "permiteComplementacao",
        "coletaInscricao", "coletaSolicitacaoIsencao", "inicio", "fim", "produtos",
        "emiteParecerIndividual", "bancasRequeridas", "regraRecurso", "unidadeResponsavel", "tiposDeDecisao", "regraDeHomologacao",
    ];

    /// <summary>
    /// O cronograma de fases: <c>origemCandidatos</c> é lido e validado para
    /// fechar a gramática, mas <b>não é propagado</b> — é atributo de raiz do agregado
    /// (<c>ProcessoSeletivo.OrigemCandidatos</c>), imutável após a criação (nenhum
    /// <c>Definir*</c> o altera), e por isso nada aqui precisa repô-lo.
    /// </summary>
    /// <param name="comId">
    /// <see langword="true"/> quando o bloco congela <c>id</c> por fase: a gramática é fechada, então o
    /// mesmo leitor não aceita as duas formas com uma única lista de chaves. Com <see langword="true"/>, a
    /// fase reidrata preservando o Id congelado (<see cref="FaseCronograma.Reidratar"/>); com
    /// <see langword="false"/>, <see cref="FaseCronograma.Criar"/> gera um Id novo.
    /// </param>
    internal static IReadOnlyList<FaseCronograma> LerCronogramaFases(LeitorEnvelope leitor, JsonObject payload, bool comId = false)
    {
        JsonObject bloco = leitor.Objeto(payload, "cronogramaFases", "$");
        leitor.ExigirChaves(bloco, "cronogramaFases", "origemCandidatos", "fases");

        leitor.Enumeracao<OrigemCandidatos>(bloco, "origemCandidatos", "cronogramaFases");

        JsonArray array = leitor.Array(bloco, "fases", "cronogramaFases");
        return leitor.Falhou ? [] : LerFases(leitor, array, comId);
    }

    private static List<FaseCronograma> LerFases(LeitorEnvelope leitor, JsonArray array, bool comId)
    {
        List<FaseCronograma> fases = [];
        for (int i = 0; i < array.Count; i++)
        {
            FaseCronograma? fase = LerFase(leitor, leitor.ItemObjeto(array, i, "cronogramaFases.fases"), $"cronogramaFases.fases[{i}]", comId);
            if (fase is null)
            {
                return [];
            }

            fases.Add(fase);
        }

        return fases;
    }

    private static FaseCronograma? LerFase(LeitorEnvelope leitor, JsonObject item, string path, bool comId)
    {
        leitor.ExigirChaves(item, path, comId ? [.. ChavesDaFase, "id"] : ChavesDaFase);

        Guid? id = comId ? leitor.Identificador(item, "id", path) : null;
        int ordem = leitor.Inteiro(item, "ordem", path);
        OrigemCanonicaDaFase origem = LerOrigemCanonica(leitor, item, path);
        DecisoesDeCronogramaDaFase decisoes = LerDecisoesSemUnidade(leitor, item, path, ordem);
        bool emiteParecerIndividual = leitor.Booleano(item, "emiteParecerIndividual", path);

        return leitor.Falhou
            ? null
            : LerFilhasEMontar(leitor, item, path, id, origem, decisoes, emiteParecerIndividual);
    }

    private static OrigemCanonicaDaFase LerOrigemCanonica(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.Identificador(item, "faseCanonicaOrigemId", path),
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.FaseCodigo),
        leitor.Enumeracao<OrigemDataFase>(item, "origemData", path),
        leitor.Booleano(item, "agrupaEtapas", path),
        leitor.Booleano(item, "permiteComplementacao", path),
        leitor.Booleano(item, "coletaInscricao", path),
        leitor.Booleano(item, "coletaSolicitacaoIsencao", path));

    // A unidade responsável vem depois das filhas na ordem da gramática, e completa estas decisões.
    private static DecisoesDeCronogramaDaFase LerDecisoesSemUnidade(
        LeitorEnvelope leitor, JsonObject item, string path, int ordem) => new(
        ordem,
        leitor.InstanteOpcional(item, "inicio", path),
        leitor.InstanteOpcional(item, "fim", path),
        null);

    private static FaseCronograma? LerFilhasEMontar(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        Guid? id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        bool emiteParecerIndividual)
    {
        IReadOnlyList<ProdutoDaFase> produtos = LerProdutosDaFase(leitor, item, path);
        IReadOnlyList<BancaRequerida> bancas = leitor.Falhou ? [] : LerBancasRequeridas(leitor, item, path);
        RegraRecursoFase? regraRecurso = leitor.Falhou ? null : LeituraDosPrazosDeRecurso.LerRegraRecursoFase(leitor, item, path);
        UnidadeResponsavelSnapshot? unidade = leitor.Falhou ? null : LerUnidadeResponsavel(leitor, item, path);
        IReadOnlyList<string> tipos = leitor.Falhou ? [] : TiposDeDecisaoNoEnvelope.Ler(leitor, item, path);
        RegraDeHomologacaoDasInscricoes? regraDeHomologacao = leitor.Falhou ? null : RegraDeHomologacaoNoEnvelope.Ler(leitor, item, path);

        return leitor.Falhou
            ? null
            : Montar(
                leitor, id, origem, decisoes with { UnidadeResponsavel = unidade },
                new(produtos, emiteParecerIndividual, regraRecurso, tipos, regraDeHomologacao), bancas);
    }

    private static FaseCronograma? Montar(
        LeitorEnvelope leitor,
        Guid? id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas)
    {
        Result<FaseCronograma> fase = ConferirEReidratar(id, origem, decisoes, publicacoes, bancas);
        return fase.IsFailure ? leitor.Propagar<FaseCronograma?>(fase.Error!) : fase.Value;
    }

    // A âncora do recurso e o recorte de competência são conferidos pelo mesmo predicado do agregado:
    // a fábrica só tem throw para esses estados, porque nenhum caminho de escrita os produz, e um
    // envelope incoerente tem de virar recusa nomeada.
    private static Result<FaseCronograma> ConferirEReidratar(
        Guid? id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas)
    {
        if (AncoraDoRecursoDaFase.Violacao(origem.Codigo, publicacoes.Produtos, publicacoes.RegraRecurso) is { } violacaoDaAncora)
        {
            return Result<FaseCronograma>.Failure(violacaoDaAncora.Error);
        }

        return RecorteDeCompetenciaDasBancas.Violacoes(origem.Codigo, bancas) is [{ } violacaoDoRecorte, ..]
            ? Result<FaseCronograma>.Failure(violacaoDoRecorte.Error)
            : Reidratar(id, origem, decisoes, publicacoes, bancas);
    }

    private static Result<FaseCronograma> Reidratar(
        Guid? id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas) => id is { } congelado
        ? Result<FaseCronograma>.Success(FaseCronograma.Reidratar(congelado, origem, decisoes, publicacoes, bancas))
        : FaseCronograma.Criar(origem, decisoes, publicacoes, bancas);

    /// <summary>
    /// A unidade que responde pela fase, congelada por valor: <c>null</c> enquanto a fase não a
    /// definiu, ou o objeto <c>{origemId, sigla, nome}</c>.
    /// </summary>
    internal static UnidadeResponsavelSnapshot? LerUnidadeResponsavel(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonObject? unidade = leitor.ObjetoOpcional(faseItem, "unidadeResponsavel", pathPai);
        if (unidade is null || leitor.Falhou)
        {
            return null;
        }

        string path = $"{pathPai}.unidadeResponsavel";
        leitor.ExigirChaves(unidade, path, "origemId", "sigla", "nome");
        Guid origemId = leitor.Identificador(unidade, "origemId", path);
        string sigla = leitor.TextoNaoVazio(unidade, "sigla", path, LimitesDoEnvelope.UnidadeAdministradoraSigla);
        string nome = leitor.TextoNaoVazio(unidade, "nome", path, LimitesDoEnvelope.UnidadeAdministradoraNome);
        return leitor.Falhou ? null : new UnidadeResponsavelSnapshot(origemId, sigla, nome);
    }

    /// <summary>
    /// Os produtos que a fase publica. O <c>id</c> é congelado e reidratado — é contra ele
    /// que o ato publicado resolverá, de volta, a configuração de recurso que lhe
    /// corresponde.
    /// </summary>
    internal static List<ProdutoDaFase> LerProdutosDaFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "produtos", pathPai);
        List<ProdutoDaFase> produtos = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            ProdutoDaFase? produto = LerProduto(leitor, leitor.ItemObjeto(array, i, $"{pathPai}.produtos"), $"{pathPai}.produtos[{i}]");
            if (produto is not null)
            {
                produtos.Add(produto);
            }
        }

        return leitor.Falhou ? [] : produtos;
    }

    private static ProdutoDaFase? LerProduto(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "id", "atoCodigo", "papel");
        Guid? id = leitor.Identificador(item, "id", path);
        string atoCodigo = leitor.TextoNaoVazio(item, "atoCodigo", path, LimitesDoEnvelope.TipoAtoCodigo);
        PapelProdutoFase? papel = leitor.EnumeracaoOpcional<PapelProdutoFase>(item, "papel", path);

        return leitor.Falhou ? null : ProdutoDaFase.Reidratar(id!.Value, atoCodigo, papel);
    }

    internal static List<BancaRequerida> LerBancasRequeridas(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "bancasRequeridas", pathPai);
        List<BancaRequerida> bancas = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            string path = $"{pathPai}.bancasRequeridas[{i}]";
            BancaRequerida? banca = LerBancaRequerida(leitor, leitor.ItemObjeto(array, i, $"{pathPai}.bancasRequeridas"), path);
            if (banca is not null)
            {
                bancas.Add(banca);
            }
        }

        return leitor.Falhou ? [] : bancas;
    }

    private static BancaRequerida? LerBancaRequerida(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "tipoBancaOrigemId", "codigo", "nome", "regraDeDeliberacao", "recorteDeCompetencia");
        BancaLida? lida = BancaLida.Ler(leitor, item, path);
        IReadOnlyList<CategoriaJulgada> recorte = lida is null ? [] : LeituraDosPrazosDeRecurso.LerRecorteDeCompetencia(leitor, item, path);

        return leitor.Falhou || lida is null
            ? null
            : BancaRequerida.Criar(lida.TipoBancaOrigemId, lida.Codigo, lida.Nome, recorte, lida.Regra);
    }
}

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
        "id", "chave", "ordem", "noMesmoMomentoDaAnterior", "tipoFaseOrigemId", "codigo", "tipoFaseNome", "origemData", "agrupaEtapas", "permiteComplementacao",
        "coletaInscricao", "coletaSolicitacaoIsencao", "inicio", "fim", "publicacoesPrevistas",
        "emiteParecerIndividual", "bancasRequeridas", "regraRecurso", "unidadeResponsavel", "tiposDeDecisao", "regraDeHomologacao",
    ];

    /// <summary>
    /// O cronograma de fases: <c>origemCandidatos</c> é lido e validado para
    /// fechar a gramática, mas <b>não é propagado</b> — é atributo de raiz do agregado
    /// (<c>ProcessoSeletivo.OrigemCandidatos</c>), imutável após a criação (nenhum
    /// <c>Definir*</c> o altera), e por isso nada aqui precisa repô-lo.
    /// </summary>
    internal static IReadOnlyList<FaseCronograma> LerCronogramaFases(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "cronogramaFases", "$");
        leitor.ExigirChaves(bloco, "cronogramaFases", "origemCandidatos", "fases");

        leitor.CodigoDeContrato<OrigemCandidatos>(bloco, "origemCandidatos", "cronogramaFases");

        JsonArray array = leitor.Array(bloco, "fases", "cronogramaFases");
        return leitor.Falhou ? [] : LerFases(leitor, array);
    }

    private static List<FaseCronograma> LerFases(LeitorEnvelope leitor, JsonArray array)
    {
        List<FaseCronograma> fases = [];
        for (int i = 0; i < array.Count; i++)
        {
            FaseCronograma? fase = LerFase(leitor, leitor.ItemObjeto(array, i, "cronogramaFases.fases"), $"cronogramaFases.fases[{i}]");
            if (fase is null)
            {
                return [];
            }

            fases.Add(fase);
        }

        ChavesNoEnvelope.ExigirUnicas(leitor, fases.Select(static fase => fase.Chave), "cronogramaFases.fases");
        return leitor.Falhou ? [] : fases;
    }

    private static FaseCronograma? LerFase(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaFase);

        Guid id = leitor.Identificador(item, "id", path);
        ChaveLocal chave = ChavesNoEnvelope.Ler(leitor, item, path);
        int ordem = leitor.Inteiro(item, "ordem", path);
        OrigemCanonicaDaFase origem = LerOrigemCanonica(leitor, item, path);
        DecisoesDeCronogramaDaFase decisoes = LerDecisoesSemUnidade(leitor, item, path, ordem);
        bool emiteParecerIndividual = leitor.Booleano(item, "emiteParecerIndividual", path);

        return leitor.Falhou
            ? null
            : LerFilhasEMontar(leitor, item, path, (id, chave), origem, decisoes, emiteParecerIndividual);
    }

    private static OrigemCanonicaDaFase LerOrigemCanonica(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.Identificador(item, "tipoFaseOrigemId", path),
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.FaseCodigo),
        leitor.CodigoDeContrato<OrigemDataFase>(item, "origemData", path),
        leitor.Booleano(item, "agrupaEtapas", path),
        leitor.Booleano(item, "permiteComplementacao", path),
        leitor.Booleano(item, "coletaInscricao", path),
        leitor.Booleano(item, "coletaSolicitacaoIsencao", path))
    { Nome = leitor.TextoNaoVazio(item, "tipoFaseNome", path, LimitesDoEnvelope.TipoFaseNome) };

    // A unidade responsável vem depois das filhas na ordem da gramática, e completa estas decisões.
    private static DecisoesDeCronogramaDaFase LerDecisoesSemUnidade(
        LeitorEnvelope leitor, JsonObject item, string path, int ordem) =>
        new(ordem, leitor.InstanteOpcional(item, "inicio", path), leitor.InstanteOpcional(item, "fim", path), null)
        {
            NoMesmoMomentoDaAnterior = leitor.Booleano(item, "noMesmoMomentoDaAnterior", path),
        };

    private static FaseCronograma? LerFilhasEMontar(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        (Guid Id, ChaveLocal Chave) identidade,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        bool emiteParecerIndividual)
    {
        IReadOnlyList<ProdutoDaFase> produtos = LerProdutosDaFase(leitor, item, path);
        BancasLidas<BancaRequerida> bancas = leitor.Falhou ? new(path) : LerBancasRequeridas(leitor, item, path);
        RegraRecursoFase? regraRecurso = leitor.Falhou ? null : LeituraDosPrazosDeRecurso.LerRegraRecursoFase(leitor, item, path);
        UnidadeResponsavelSnapshot? unidade = leitor.Falhou ? null : LerUnidadeResponsavel(leitor, item, path);
        IReadOnlyList<string> tipos = leitor.Falhou ? [] : TiposDeDecisaoNoEnvelope.Ler(leitor, item, path);
        RegraDeHomologacaoDasInscricoes? regraDeHomologacao = leitor.Falhou ? null : RegraDeHomologacaoNoEnvelope.Ler(leitor, item, path);

        return leitor.Falhou
            ? null
            : Montar(
                leitor, identidade, origem, decisoes with { UnidadeResponsavel = unidade },
                new(produtos, emiteParecerIndividual, regraRecurso, tipos, regraDeHomologacao), bancas);
    }

    private static FaseCronograma? Montar(
        LeitorEnvelope leitor,
        (Guid Id, ChaveLocal Chave) identidade,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        BancasLidas<BancaRequerida> bancas)
    {
        Result<FaseCronograma> fase = ConferirEReidratar(identidade, origem, decisoes, publicacoes, bancas.Bancas);
        if (fase.IsFailure)
        {
            return leitor.Propagar<FaseCronograma?>(fase.Error!);
        }

        bancas.ExigirQueSeguemARaiz(leitor, fase.Value!.BancasRequeridas.ToDictionary(static banca => banca.Id, static banca => banca.PosicaoNoTipo));

        return leitor.Falhou ? null : fase.Value;
    }

    // A âncora do recurso e os candidatos atendidos são conferidos pelo mesmo predicado do agregado:
    // a fábrica só tem throw para esses estados, porque nenhum caminho de escrita os produz, e um
    // envelope incoerente tem de virar recusa nomeada.
    private static Result<FaseCronograma> ConferirEReidratar(
        (Guid Id, ChaveLocal Chave) identidade,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas)
    {
        if (AncoraDoRecursoDaFase.Violacao(publicacoes.Produtos, publicacoes.RegraRecurso) is { } violacaoDaAncora)
        {
            return Result<FaseCronograma>.Failure(violacaoDaAncora.Error);
        }

        return CandidatosAtendidosPelasBancas.Violacoes(bancas) is [{ } violacaoDosCandidatosAtendidos, ..]
            ? Result<FaseCronograma>.Failure(violacaoDosCandidatosAtendidos.Error)
            : Reidratar(identidade, origem, decisoes, publicacoes, bancas);
    }

    private static Result<FaseCronograma> Reidratar(
        (Guid Id, ChaveLocal Chave) identidade,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas) =>
        Result<FaseCronograma>.Success(FaseCronograma.Reidratar(identidade.Chave, identidade.Id, origem, decisoes, publicacoes, bancas));

    /// <summary>
    /// A unidade que responde pela fase, congelada por valor: <c>null</c> enquanto a fase não a
    /// definiu, ou o objeto <c>{origemId, codigo, sigla, nome}</c>.
    /// </summary>
    internal static UnidadeResponsavelSnapshot? LerUnidadeResponsavel(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonObject? unidade = leitor.ObjetoOpcional(faseItem, "unidadeResponsavel", pathPai);
        if (unidade is null || leitor.Falhou)
        {
            return null;
        }

        string path = $"{pathPai}.unidadeResponsavel";
        leitor.ExigirChaves(unidade, path, "origemId", "codigo", "sigla", "nome");
        Guid origemId = leitor.Identificador(unidade, "origemId", path);
        string codigo = leitor.TextoNaoVazio(unidade, "codigo", path, LimitesDoEnvelope.UnidadeAdministradoraCodigo);
        string sigla = leitor.TextoNaoVazio(unidade, "sigla", path, LimitesDoEnvelope.UnidadeAdministradoraSigla);
        string nome = leitor.TextoNaoVazio(unidade, "nome", path, LimitesDoEnvelope.UnidadeAdministradoraNome);
        return leitor.Falhou ? null : new UnidadeResponsavelSnapshot(origemId, codigo, sigla, nome);
    }

    /// <summary>
    /// Os produtos que a fase publica. O <c>id</c> é congelado e reidratado — é contra ele
    /// que o ato publicado resolverá, de volta, a configuração de recurso que lhe
    /// corresponde.
    /// </summary>
    internal static List<ProdutoDaFase> LerProdutosDaFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "publicacoesPrevistas", pathPai);
        List<ProdutoDaFase> produtos = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            ProdutoDaFase? produto = LerProduto(leitor, leitor.ItemObjeto(array, i, $"{pathPai}.publicacoesPrevistas"), $"{pathPai}.publicacoesPrevistas[{i}]");
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
        PapelDaPublicacaoPrevista? papel = leitor.CodigoDeContratoOpcional<PapelDaPublicacaoPrevista>(item, "papel", path);

        return leitor.Falhou ? null : ProdutoDaFase.Reidratar(id!.Value, atoCodigo, papel);
    }

    internal static BancasLidas<BancaRequerida> LerBancasRequeridas(LeitorEnvelope leitor, JsonObject faseItem, string pathPai) =>
        BancasLidas<BancaRequerida>.Ler(leitor, leitor.Array(faseItem, "bancasRequeridas", pathPai), $"{pathPai}.bancasRequeridas", LerBancaRequerida);

    private static (BancaRequerida, BancaLida)? LerBancaRequerida(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, [.. TipoDeBancaNoEnvelope.Chaves, "id", "chave", "posicaoNoTipo", "regraDeDeliberacao", "candidatosAtendidos"]);
        BancaLida? lida = BancaLida.Ler(leitor, item, path);
        IReadOnlyList<CategoriaJulgada> atendidos = lida is null ? [] : LeituraDosPrazosDeRecurso.LerCandidatosAtendidos(leitor, item, path);

        return leitor.Falhou || lida is null
            ? null
            : (BancaRequerida.ReconstruirDosDadosSalvos(lida.Chave, lida.Id, lida.Tipo, atendidos, lida.Regra), lida);
    }
}

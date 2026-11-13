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
/// Leitura das fases do cronograma.
/// </summary>
internal static class LeituraDoCronograma
{
    private static readonly string[] ChavesDaFase =
    [
        "ordem", "faseCanonicaOrigemId", "codigo", "origemData", "agrupaEtapas", "permiteComplementacao",
        "coletaInscricao", "coletaSolicitacaoIsencao", "inicio", "fim", "produtos", "faseConcluinteCodigo",
        "emiteParecerIndividual", "bancasRequeridas", "regraRecurso", "unidadeResponsavel",
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
        if (leitor.Falhou)
        {
            return [];
        }

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

        DeclaracaoDaFase d = LerDeclaracao(leitor, item, path, comId);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<ProdutoDaFase> produtos = LerProdutosDaFase(leitor, item, path);
        IReadOnlyList<BancaRequerida> bancas = leitor.Falhou ? [] : LerBancasRequeridas(leitor, item, path);
        RegraRecursoFase? regraRecurso = leitor.Falhou ? null : LeituraDosPrazosDeRecurso.LerRegraRecursoFase(leitor, item, path);
        UnidadeResponsavelSnapshot? unidadeResponsavel = leitor.Falhou ? null : LerUnidadeResponsavel(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        // A âncora do recurso e o recorte de competência são conferidos pelo mesmo predicado do agregado:
        // a fábrica só tem throw para esses estados, porque nenhum caminho de escrita os produz, e um
        // envelope incoerente tem de virar recusa nomeada.
        if (FaseCronograma.ViolacaoDaAncoraDoRecurso(d.Codigo, produtos, regraRecurso) is { } violacaoDaAncora)
        {
            return leitor.Propagar<FaseCronograma?>(violacaoDaAncora.Error);
        }

        if (FaseCronograma.ViolacoesDoRecorteDeCompetencia(d.Codigo, bancas) is [{ } violacaoDoRecorte, ..])
        {
            return leitor.Propagar<FaseCronograma?>(violacaoDoRecorte.Error);
        }

        Result<FaseCronograma> fase = Reidratar(d, produtos, bancas, regraRecurso, unidadeResponsavel);
        return fase.IsFailure ? leitor.Propagar<FaseCronograma?>(fase.Error!) : fase.Value;
    }

    private static Result<FaseCronograma> Reidratar(
        DeclaracaoDaFase d,
        IReadOnlyList<ProdutoDaFase> produtos,
        IReadOnlyList<BancaRequerida> bancas,
        RegraRecursoFase? regraRecurso,
        UnidadeResponsavelSnapshot? unidadeResponsavel) => d.Id is { } id
        ? Result<FaseCronograma>.Success(FaseCronograma.Reidratar(
            id, d.Ordem, d.FaseCanonicaOrigemId, d.Codigo, d.OrigemData,
            d.AgrupaEtapas, d.PermiteComplementacao, d.ColetaInscricao, d.ColetaSolicitacaoIsencao,
            d.Inicio, d.Fim, produtos, d.FaseConcluinteCodigo, d.EmiteParecerIndividual, bancas, regraRecurso, unidadeResponsavel))
        : FaseCronograma.Criar(
            d.Ordem, d.FaseCanonicaOrigemId, d.Codigo, d.OrigemData,
            d.AgrupaEtapas, d.PermiteComplementacao, d.ColetaInscricao, d.ColetaSolicitacaoIsencao,
            d.Inicio, d.Fim, produtos, d.FaseConcluinteCodigo, d.EmiteParecerIndividual, bancas, regraRecurso, unidadeResponsavel);

    private static DeclaracaoDaFase LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path, bool comId) => new()
    {
        Id = comId ? leitor.Identificador(item, "id", path) : null,
        Ordem = leitor.Inteiro(item, "ordem", path),
        FaseCanonicaOrigemId = leitor.Identificador(item, "faseCanonicaOrigemId", path),
        Codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.FaseCodigo),
        OrigemData = leitor.Enumeracao<OrigemDataFase>(item, "origemData", path),
        AgrupaEtapas = leitor.Booleano(item, "agrupaEtapas", path),
        PermiteComplementacao = leitor.Booleano(item, "permiteComplementacao", path),
        ColetaInscricao = leitor.Booleano(item, "coletaInscricao", path),
        ColetaSolicitacaoIsencao = leitor.Booleano(item, "coletaSolicitacaoIsencao", path),
        Inicio = leitor.InstanteOpcional(item, "inicio", path),
        Fim = leitor.InstanteOpcional(item, "fim", path),
        FaseConcluinteCodigo = leitor.TextoOpcional(item, "faseConcluinteCodigo", path, LimitesDoEnvelope.FaseCodigo),
        EmiteParecerIndividual = leitor.Booleano(item, "emiteParecerIndividual", path),
    };

    private sealed class DeclaracaoDaFase
    {
        public Guid? Id { get; init; }

        public int Ordem { get; init; }

        public Guid FaseCanonicaOrigemId { get; init; }

        public string Codigo { get; init; } = string.Empty;

        public OrigemDataFase OrigemData { get; init; }

        public bool AgrupaEtapas { get; init; }

        public bool PermiteComplementacao { get; init; }

        public bool ColetaInscricao { get; init; }

        public bool ColetaSolicitacaoIsencao { get; init; }

        public DateTimeOffset? Inicio { get; init; }

        public DateTimeOffset? Fim { get; init; }

        public string? FaseConcluinteCodigo { get; init; }

        public bool EmiteParecerIndividual { get; init; }
    }

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
        if (leitor.Falhou)
        {
            return [];
        }

        List<ProdutoDaFase> produtos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.produtos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.produtos");
            leitor.ExigirChaves(item, path, "id", "atoCodigo", "papel");

            Guid? id = leitor.Identificador(item, "id", path);
            string atoCodigo = leitor.TextoNaoVazio(item, "atoCodigo", path, LimitesDoEnvelope.TipoAtoCodigo);
            PapelProdutoFase? papel = leitor.EnumeracaoOpcional<PapelProdutoFase>(item, "papel", path);

            if (leitor.Falhou)
            {
                return [];
            }

            produtos.Add(ProdutoDaFase.Reidratar(id!.Value, atoCodigo, papel));
        }

        return produtos;
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

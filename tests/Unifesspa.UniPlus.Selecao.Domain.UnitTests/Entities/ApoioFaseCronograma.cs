namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioFaseCronograma
{
    internal static BancaRequerida Banca(string codigo, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(),
            codigo,
            "Banca de teste",
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    internal static ReferenciaRegra RegraAncorada() =>
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64));

    internal static RegraRecursoFase Recurso(Guid produtoAncoraId) =>
        RegraRecursoFase.Criar(RegraAncorada(), ParametrosValidos(), produtoAncoraId).Value!;

    internal static FaseCronograma Reidratar(
        IReadOnlyList<ProdutoDaFase> produtos,
        RegraRecursoFase? regraRecurso,
        IReadOnlyList<BancaRequerida>? bancasRequeridas = null) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos, EmiteParecerIndividual: false, regraRecurso),
            bancasRequeridas ?? []);

    internal static ParametrosRegraPrazoRecurso ParametrosValidos() => new(
        PrazoValor: 48m,
        PrazoUnidade: UnidadePrazo.Horas,
        SuspensividadePrimeiraInstanciaValor: null,
        SuspensividadePrimeiraInstanciaUnidade: null,
        AdmiteInstanciaSuperior: false,
        SuspensividadeSegundaInstanciaValor: null,
        SuspensividadeSegundaInstanciaUnidade: null);

    internal static Result<FaseCronograma> Criar(
        int ordem = 1,
        OrigemDataFase origemData = OrigemDataFase.NoEdital,
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        bool agrupaEtapas = false,
        IReadOnlyList<ProdutoDaFase>? produtos = null,
        string? faseConcluinteCodigo = null,
        bool emiteParecerIndividual = false,
        IReadOnlyList<BancaRequerida>? bancasRequeridas = null,
        RegraRecursoFase? regraRecurso = null,
        bool semUnidadeResponsavel = false,
        UnidadeResponsavelSnapshot? unidadeResponsavel = null) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", origemData, agrupaEtapas, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, inicio, fim, faseConcluinteCodigo, semUnidadeResponsavel ? null : unidadeResponsavel ?? UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos ?? [], emiteParecerIndividual, regraRecurso),
            bancasRequeridas ?? []);
}

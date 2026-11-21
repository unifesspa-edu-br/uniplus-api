namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
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

    internal static ParametrosDeclaradosDoRecurso ParametrosValidos() => ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas);

    /// <summary>Cria a fase de uma data no edital; o teste informa só o que difere dela, com <c>ajuste</c>.</summary>
    internal static Result<FaseCronograma> Criar(Func<ModeloDeFase, ModeloDeFase>? ajuste = null)
    {
        ModeloDeFase modelo = new ModeloDeFase("RESULTADO_PRELIMINAR", 1) { OrigemData = OrigemDataFase.NoEdital };
        modelo = ajuste?.Invoke(modelo) ?? modelo;

        return FaseCronograma.Criar(modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas);
    }
}

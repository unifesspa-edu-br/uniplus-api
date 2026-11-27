namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AlteracaoDoCronogramaEmRascunhoTests
{
    [Fact(DisplayName = "Remover uma fase em rascunho conserva no cronograma somente a fase mantida")]
    public void DefinirCronogramaFases_FaseRemovida_ConservaAFaseMantida()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FaseCronograma mantida = processo.CronogramaFases.Single();
        FaseCronograma removida = Fase(2, "CLASSIFICACAO");
        processo.DefinirCronogramaFases([mantida, removida], []).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([mantida], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be(mantida.Codigo);
    }

    [Fact(DisplayName = "Trocar a unidade da fase em rascunho guarda a nova unidade sem conjunto de fases iniciadas")]
    public void DefinirCronogramaFases_UnidadeTrocada_GuardaANovaUnidade()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FaseCronograma original = processo.CronogramaFases.Single();
        original.UnidadeResponsavel!.OrigemId.Should().Be(UnidadesDeTeste.Ceps.OrigemId);
        FaseCronograma trocada = ComUnidade(original, UnidadesDeTeste.Cga);

        Result resultado = processo.DefinirCronogramaFases([trocada], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.UnidadeResponsavel!.OrigemId.Should().Be(UnidadesDeTeste.Cga.OrigemId);
    }

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false,
                permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                ordem, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null), []).Value!;

    private static FaseCronograma ComUnidade(FaseCronograma atual, UnidadeResponsavelSnapshot unidade) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(
                atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData, atual.AgrupaEtapas,
                atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(atual.Ordem, atual.Inicio, atual.Fim, unidade),
            new PublicacoesDaFase([.. atual.Produtos], atual.EmiteParecerIndividual, atual.RegraRecurso),
            [.. atual.BancasRequeridas]).Value!;
}

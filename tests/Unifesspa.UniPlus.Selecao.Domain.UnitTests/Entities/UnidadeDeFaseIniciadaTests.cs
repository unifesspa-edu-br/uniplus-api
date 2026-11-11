namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class UnidadeDeFaseIniciadaTests
{
    [Fact(DisplayName = "Trocar a unidade de uma fase iniciada é recusado, citando a fase")]
    public void TrocarUnidade_FaseIniciada_Recusa()
    {
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme();
        ProcessoSeletivo processo = ProcessoComCronograma(fase);

        Result resultado = processo.DefinirCronogramaFases(
            [ComUnidade(fase, UnidadesDeTeste.Cga)], [], PrecondicaoIfMatch.Curinga,
            new FasesIniciadas([fase.FaseCanonicaOrigemId]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.UnidadeDeFaseIniciadaNaoMuda");
        resultado.Error.Message.Should().Contain("RESULTADO_FINAL");
        processo.CronogramaFases.Single().UnidadeResponsavelOrigemId.Should().Be(UnidadesDeTeste.Ceps.OrigemId);
    }

    [Fact(DisplayName = "Trocar a unidade de uma fase que não começou é aceito")]
    public void TrocarUnidade_FaseNaoIniciada_Aceita()
    {
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme();
        ProcessoSeletivo processo = ProcessoComCronograma(fase);

        Result resultado = processo.DefinirCronogramaFases(
            [ComUnidade(fase, UnidadesDeTeste.Cga)], [], PrecondicaoIfMatch.Curinga, FasesIniciadas.Nenhuma);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().UnidadeResponsavelOrigemId.Should().Be(UnidadesDeTeste.Cga.OrigemId);
    }

    [Fact(DisplayName = "Redefinir o cronograma sem trocar a unidade de uma fase iniciada é aceito")]
    public void MesmaUnidade_FaseIniciada_Aceita()
    {
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme();
        ProcessoSeletivo processo = ProcessoComCronograma(fase);

        Result resultado = processo.DefinirCronogramaFases(
            [ComUnidade(fase, UnidadesDeTeste.Ceps)], [], PrecondicaoIfMatch.Curinga,
            new FasesIniciadas([fase.FaseCanonicaOrigemId]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Sem informar as fases iniciadas, nenhuma conta como iniciada")]
    public void SemInformarIniciadas_NenhumaFaseConta()
    {
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme();
        ProcessoSeletivo processo = ProcessoComCronograma(fase);

        processo.DefinirCronogramaFases([ComUnidade(fase, UnidadesDeTeste.Cga)], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O conjunto de fases iniciadas responde só pelas fases que recebeu")]
    public void FasesIniciadas_ContemSoAsRecebidas()
    {
        Guid iniciada = Guid.CreateVersion7();
        FasesIniciadas conjunto = new([iniciada]);

        conjunto.Contem(iniciada).Should().BeTrue();
        conjunto.Contem(Guid.CreateVersion7()).Should().BeFalse();
        FasesIniciadas.Nenhuma.Contem(iniciada).Should().BeFalse();
    }

    private static ProcessoSeletivo ProcessoComCronograma(FaseCronograma fase)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Unidade da fase", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma ComUnidade(FaseCronograma atual, UnidadeResponsavelSnapshot unidade) =>
        FaseCronograma.Criar(
            atual.Ordem, atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData,
            atual.AgrupaEtapas, atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao,
            atual.Inicio, atual.Fim, [.. atual.Produtos], atual.FaseConcluinteCodigo, atual.EmiteParecerIndividual,
            [.. atual.BancasRequeridas], atual.RegraRecurso, unidade).Value!;
}

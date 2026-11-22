namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ApoioDasBancasDoProcesso;

/// <summary>
/// A banca cujo papel julga recurso e decide por ato individual declara a votação do recurso: quórum, critério e encerramento
/// juntos. A recusa sai na publicação, com o código da regra de deliberação obrigatória, e quem decide é o papel da cópia do tipo
/// que a banca guarda.
/// </summary>
public sealed class VotacaoDoRecursoDaBancaTests
{
    private const string CodigoDaRecusa = "Banca.RegraDeDeliberacaoObrigatoria";

    private static RegraDeDeliberacao AtoIndividualSemVotacao() =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(ModoDeDeliberacao.AtoIndividual.Codigo(), 1, 0, new(null, null, null, null, null))).Value!;

    private static RegraDeDeliberacao AtoIndividualSoComOQuorum() =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(ModoDeDeliberacao.AtoIndividual.Codigo(), 3, 1, new(2, null, null, null, null))).Value!;

    private static RegraDeDeliberacao AtoIndividualComVotacao() =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.AtoIndividual.Codigo(), 3, 1, new(2, CriterioDeVotacao.MaioriaSimples.Codigo(), DesempateDaVotacao.VotoDaPresidencia.Codigo(), EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null))).Value!;

    private static TipoDeBancaCopiado ComPapel(PapelDaBanca papel) => TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursos, papel);

    private static IReadOnlyList<FieldError> RecusasDaFase(PapelDaBanca papel, RegraDeDeliberacao regra) =>
        RecusasDaFase(ComPapel(papel), regra);

    private static IReadOnlyList<FieldError> RecusasDaFase(TipoDeBancaCopiado tipo, RegraDeDeliberacao? regra)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: ProcessoConformeFactory.FaseConforme(bancas: [BancaRequerida.Criar(tipo, [], regra)]));

        return ConferenciaDasBancas.ConferirRegrasDeDeliberacao(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas));
    }

    private static IReadOnlyList<FieldError> RecusasDaEtapa(PapelDaBanca papel, RegraDeDeliberacao regra) =>
        RecusasDaEtapa(ComPapel(papel), regra);

    private static IReadOnlyList<FieldError> RecusasDaEtapa(TipoDeBancaCopiado tipo, RegraDeDeliberacao? regra)
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([BancaDaEtapa.Criar(tipo, regra)]);

        return ConferenciaDasBancas.ConferirRegrasDeDeliberacao(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas));
    }

    [Fact(DisplayName = "A banca da fase que julga recurso e decide por ato individual sem votação é recusada, citando o papel")]
    public void BancaDaFase_JulgaRecursoSemVotacao_Recusa()
    {
        IReadOnlyList<FieldError> recusas = RecusasDaFase(PapelDaBanca.JulgaAPrimeiraInstancia, AtoIndividualSemVotacao());

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        recusa.Error.Code.Should().Be(CodigoDaRecusa);
        recusa.Error.Message.Should().Be(
            "A banca 'Banca de análise de recursos 1' da fase 'RESULTADO_FINAL' decide por ato individual e julga recurso com o papel 'Julga o recurso em 1ª instância', mas não declara a votação do recurso. Declare o quórum, o critério e o encerramento usados no julgamento do recurso.");
    }

    [Fact(DisplayName = "A banca da etapa que julga recurso e decide por ato individual sem votação é recusada, citando a posição da etapa")]
    public void BancaDaEtapa_JulgaRecursoSemVotacao_Recusa()
    {
        IReadOnlyList<FieldError> recusas = RecusasDaEtapa(PapelDaBanca.JulgaAPrimeiraInstancia, AtoIndividualSemVotacao());

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("etapas[0].bancas[0]");
        recusa.Error.Code.Should().Be(CodigoDaRecusa);
        recusa.Error.Message.Should().Contain("A banca 'Banca de análise de recursos 1' da etapa na posição 1 decide por ato individual");
    }

    [Fact(DisplayName = "A votação do recurso só com o quórum é recusada: o critério e o encerramento também são exigidos")]
    public void BancaQueJulgaRecurso_SoComOQuorum_Recusa()
    {
        RecusasDaFase(PapelDaBanca.JulgaAPrimeiraInstancia, AtoIndividualSoComOQuorum()).Select(static r => r.Error.Code)
            .Should().Equal(CodigoDaRecusa);
    }

    [Fact(DisplayName = "A banca que julga recurso e declara a votação inteira por ato individual não é recusada")]
    public void BancaQueJulgaRecurso_ComVotacaoInteira_NaoRecusa()
    {
        RecusasDaFase(PapelDaBanca.JulgaAPrimeiraInstancia, AtoIndividualComVotacao()).Should().BeEmpty();
        RecusasDaEtapa(PapelDaBanca.JulgaAPrimeiraInstancia, AtoIndividualComVotacao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de 2ª instância que declara a votação do recurso inteira por ato individual, na fase e na etapa, não é recusada")]
    public void BancaDeSegundaInstancia_ComVotacaoInteira_NaoRecusa()
    {
        TipoDeBancaCopiado tipo = TiposDeBancaCopiadosDeTeste.DeSegundaInstancia();

        RecusasDaFase(tipo, AtoIndividualComVotacao()).Should().BeEmpty();
        RecusasDaEtapa(tipo, AtoIndividualComVotacao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de 2ª instância sem regra de deliberação é recusada por regra de deliberação obrigatória")]
    public void BancaDeSegundaInstancia_SemRegra_RecusaARegraAusente()
    {
        FieldError recusa = RecusasDaFase(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), null).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        recusa.Error.Code.Should().Be(CodigoDaRecusa);
        recusa.Error.Message.Should().Be("A banca 'Banca de análise de recursos de 2ª instância 1' da fase 'RESULTADO_FINAL' não declara a regra de deliberação.");
    }

    [Theory(DisplayName = "O papel que não julga recurso decide por ato individual sem votação sem ser recusado")]
    [InlineData(PapelDaBanca.AvaliaOMerito)]
    [InlineData(PapelDaBanca.ImportaAsNotasDoInep)]
    [InlineData(PapelDaBanca.ImportaCandidatos)]
    public void BancaQueNaoJulgaRecurso_AtoIndividualSemVotacao_NaoRecusa(PapelDaBanca papel)
    {
        RecusasDaFase(papel, AtoIndividualSemVotacao()).Should().BeEmpty();
        RecusasDaEtapa(papel, AtoIndividualSemVotacao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A recusa é pelo papel da cópia, e não pelo tipo: no mesmo lugar, a cópia de outro papel do mesmo tipo não é recusada")]
    public void MesmoTipo_CopiasDePapeisDiferentes_CadaBancaPelaPropriaCopia()
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas(
        [
            BancaDaEtapa.Criar(ComPapel(PapelDaBanca.AvaliaOMerito), AtoIndividualSemVotacao()),
            BancaDaEtapa.Criar(ComPapel(PapelDaBanca.JulgaAPrimeiraInstancia), AtoIndividualSemVotacao()),
        ]);

        IReadOnlyList<FieldError> recusas = ConferenciaDasBancas.ConferirRegrasDeDeliberacao(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas));

        recusas.Should().ContainSingle().Which.Field.Should().Be("etapas[0].bancas[1]");
        recusas[0].Error.Message.Should().Contain("'Banca de análise de recursos 2'");
    }

    [Fact(DisplayName = "A conferência da publicação acusa a banca que julga recurso por ato individual sem votação")]
    public void ConferenciaDaPublicacao_JulgaRecursoSemVotacao_Acusa()
    {
        BancaRequerida banca = BancaRequerida.Criar(ComPapel(PapelDaBanca.JulgaAPrimeiraInstancia), [], AtoIndividualSemVotacao());
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso(), bancas: [banca]));

        CodigosDaConferencia(processo, CorretoresDeTeste.SemConsolidacao).Should().Contain(CodigoDaRecusa);
    }
}

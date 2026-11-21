namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// O recurso que admite instância superior sobre item avaliado por banca cujo tipo encerra o recurso na 1ª instância é recusado.
/// A marca é da cópia do tipo que cada avaliadora guarda, e vale haja ou não banca dedicada a julgar o recurso.
/// </summary>
public sealed class InstanciaSuperiorNaoAdmitidaTests
{
    private const string Fase = "AVALIACAO";

    private static readonly ProcedimentoDeRecursoDeclarado[] DaHeteroidentificacao = [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao)];

    private static readonly TipoDeBancaCopiado QueEncerra =
        Avaliadora(TipoDeBanca.Heteroidentificacao, true, TipoMotivoDeDecisao.Heteroidentificacao);

    private static IReadOnlyList<FieldError> Recusas(ModeloDeFase fase, params ModeloDeEtapa[] etapas) =>
        QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(Fases(fase), Etapas(etapas));

    [Fact(DisplayName = "O recurso da fase que admite instância superior sobre item que se encerra na 1ª é recusado, citando a banca e o item")]
    public void RecursoDaFase_AdmiteInstanciaSuperior_RecusaCitandoABancaEOItem()
    {
        ModeloDeEtapa etapa = EtapaNaFase(Fase) with { Bancas = [NaEtapa(QueEncerra)] };

        FieldError recusa = Recusas(FaseComRecurso(Fase, DaHeteroidentificacao, true), etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].regraRecurso.admiteInstanciaSuperior");
        recusa.Error.Code.Should().Be("Banca.TipoNaoAdmiteInstanciaSuperior");
        recusa.Error.Message.Should().Be(
            "O recurso da fase 'AVALIACAO' admite instância superior, mas o recurso do item avaliado pela banca 'Banca de heteroidentificação 1' (Heteroidentificação) se encerra na 1ª instância. Declare que o recurso não admite instância superior.");
    }

    [Fact(DisplayName = "O recurso da etapa que admite instância superior sobre item que se encerra na 1ª é recusado, na posição do recurso")]
    public void RecursoDaEtapa_AdmiteInstanciaSuperior_RecusaNaPosicaoDoRecurso()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, true, NaEtapa(QueEncerra));

        FieldError recusa = Recusas(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].recursos[0].admiteInstanciaSuperior");
        recusa.Error.Message.Should().StartWith("O recurso da etapa na posição 1 admite instância superior, mas o recurso do item avaliado pela banca 'Banca de heteroidentificação 1'");
    }

    [Fact(DisplayName = "O recurso que não admite instância superior não é recusado, ainda que o tipo encerre o recurso")]
    public void RecursoSemInstanciaSuperior_SemRecusa()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, false, NaEtapa(QueEncerra));

        Recusas(new ModeloDeFase(Fase, 1), etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "A avaliadora de tipo que não encerra o recurso na 1ª instância não recusa a instância superior")]
    public void AvaliadoraQueNaoEncerra_SemRecusa()
    {
        TipoDeBancaCopiado semMarca = Avaliadora(TipoDeBanca.Heteroidentificacao, false, TipoMotivoDeDecisao.Heteroidentificacao);

        Recusas(new ModeloDeFase(Fase, 1), EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, true, NaEtapa(semMarca))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca dedicada a julgar o recurso não afasta a recusa: a marca é de quem avaliou o item")]
    public void ComBancaDedicada_AindaRecusa()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, true, NaEtapa(QueEncerra), BancasDeTeste.DeRecursosDaEtapa());

        Recusas(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle();
    }

    [Fact(DisplayName = "A avaliadora de outro item não conta: o recurso é do item que ela avaliou")]
    public void AvaliadoraDeOutroTipoDeDecisao_SemRecusa()
    {
        ProcedimentoDeRecursoDeclarado[] daNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

        Recusas(new ModeloDeFase(Fase, 1), EtapaComRecurso("Prova", Fase, daNota, true, NaEtapa(QueEncerra))).Should().BeEmpty();
    }

    [Fact(DisplayName = "O recurso sem procedimento ou com alvo em área do ENEM não tem avaliadora, e por isso não é recusado")]
    public void RecursoSemAvaliadora_SemRecusa()
    {
        ProcedimentoDeRecursoDeclarado[] daArea = [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica))];

        Recusas(new ModeloDeFase(Fase, 1), EtapaComRecurso("A", Fase, [], true, NaEtapa(QueEncerra)), EtapaComRecurso("B", Fase, daArea, true, NaEtapa(QueEncerra))).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada recurso é recusado uma vez, citando a primeira banca que encerra o recurso")]
    public void UmaRecusaPorRecurso_ComABancaMaisAntiga()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, true, NaEtapa(QueEncerra), NaEtapa(QueEncerra));

        IReadOnlyList<FieldError> recusas = Recusas(new ModeloDeFase(Fase, 1), etapa);

        recusas.Should().ContainSingle().Which.Error.Message.Should().Contain("'Banca de heteroidentificação 1'");
    }
}

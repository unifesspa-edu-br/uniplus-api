namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Na configuração declarada, o formulário da inscrição é o obrigatório ligado à fase do cronograma que coleta
/// a inscrição; o ainda sem fase, no rascunho, não é lido como da inscrição.
/// </summary>
public sealed class FormulariosDaInscricaoNaConfiguracaoTests
{
    private readonly ProcessoSeletivo _processo = ProcessoConformeFactory.Criar();

    private Guid FaseDaInscricao => _processo.CronogramaFases.Single(static fase => fase.ColetaInscricao).Id;

    private bool EhDaInscricao(Guid? faseId, bool obrigatorio) =>
        _processo.Declaracao().EhDaInscricao(new(CodigoDoFormulario.Criar("OUTRO").Value!, faseId, obrigatorio));

    [Fact(DisplayName = "O formulário obrigatório ligado à fase de inscrição é da inscrição")]
    public void ObrigatorioNaFaseDeInscricao_EDaInscricao() =>
        EhDaInscricao(FaseDaInscricao, obrigatorio: true).Should().BeTrue();

    [Fact(DisplayName = "O formulário ainda sem fase não é da inscrição")]
    public void SemFase_NaoEDaInscricao() =>
        EhDaInscricao(null, obrigatorio: true).Should().BeFalse();

    [Fact(DisplayName = "O formulário facultativo na fase de inscrição não é da inscrição")]
    public void Facultativo_NaoEDaInscricao() =>
        EhDaInscricao(FaseDaInscricao, obrigatorio: false).Should().BeFalse();

    [Fact(DisplayName = "O formulário numa fase fora do cronograma não é da inscrição")]
    public void FaseForaDoCronograma_NaoEDaInscricao() =>
        EhDaInscricao(Guid.NewGuid(), obrigatorio: true).Should().BeFalse();

    [Fact(DisplayName = "Os formulários do processo que são da inscrição são os obrigatórios da fase de inscrição")]
    public void DaInscricao_TrazSoOsDaInscricao() =>
        _processo.Declaracao().DaInscricao().Select(static f => f.Codigo.Valor).Should().Equal(FormularioDeTeste.CodigoDaInscricao);
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O momento da fase no cronograma (UNI-REQ-0106, UNI-REQ-0144): a ordem dela, exceto a fase da janela
/// de isenção, que é contemporânea da inscrição com que abre.
/// </summary>
public sealed class MomentoDaFaseTests
{
    [Fact(DisplayName = "A janela de isenção em fase própria tem o momento da inscrição")]
    public void JanelaDeIsencaoOrdenadaAntesDaInscricao_TemOMomentoDaInscricao()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);

        MomentoDaFase.Da(isencao, [isencao, inscricao]).Should().Be(MomentoDaFase.Da(inscricao, [isencao, inscricao]));
        MomentoDaFase.Da(isencao, [isencao, inscricao]).Should().Be(2);
    }

    [Fact(DisplayName = "A fase comum usa a própria ordem e a habilitação é posterior à inscrição")]
    public void FaseComum_UsaAPropriaOrdem()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");
        FaseCronograma[] cronograma = [inscricao, habilitacao];

        MomentoDaFase.Da(habilitacao, cronograma).Should().Be(5);
        MomentoDaFase.Da(habilitacao, cronograma).Should().BeGreaterThan(MomentoDaFase.Da(inscricao, cronograma));
    }

    [Fact(DisplayName = "A janela de isenção depois da inscrição mantém a própria ordem")]
    public void JanelaDeIsencaoDepoisDaInscricao_MantemAOrdem()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(3, "ISENCAO", coletaSolicitacaoIsencao: true);

        MomentoDaFase.Da(isencao, [inscricao, isencao]).Should().Be(3);
    }

    [Fact(DisplayName = "Havendo mais de uma fase de inscrição, a janela acompanha a de menor ordem")]
    public void MaisDeUmaFaseDeInscricao_VaiAPelaMenorOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(6, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(4, "INSCRICAO", coletaInscricao: true);

        MomentoDaFase.Da(isencao, [isencao, segundaInscricao, primeiraInscricao]).Should().Be(4);
    }

    [Fact(DisplayName = "Sem fase de inscrição, a janela de isenção usa a própria ordem")]
    public void SemFaseDeInscricao_UsaAPropriaOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);

        MomentoDaFase.Da(isencao, [isencao]).Should().Be(2);
    }
}

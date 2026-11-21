namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A vez em que o candidato envia o formulário de uma fase (UNI-REQ-0144, UNI-REQ-0149): a posição da fase na lista, exceto
/// o pedido de isenção ordenado antes da inscrição, que só abre depois do envio dela.
/// </summary>
public sealed class MomentoDoEnvioTests
{
    [Fact(DisplayName = "O pedido de isenção ordenado antes da inscrição envia na vez da inscrição")]
    public void IsencaoOrdenadaAntesDaInscricao_EnviaNaVezDaInscricao()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);

        MomentoDoEnvio.Da(isencao, [isencao, inscricao]).Should().Be(MomentoDoEnvio.Da(inscricao, [isencao, inscricao]));
        MomentoDoEnvio.Da(isencao, [isencao, inscricao]).Should().Be(2);
    }

    [Fact(DisplayName = "A fase comum envia na própria posição, e a habilitação envia depois da inscrição")]
    public void FaseComum_EnviaNaPropriaPosicao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");
        FaseCronograma[] cronograma = [inscricao, habilitacao];

        MomentoDoEnvio.Da(habilitacao, cronograma).Should().Be(5);
        MomentoDoEnvio.Da(habilitacao, cronograma).Should().BeGreaterThan(MomentoDoEnvio.Da(inscricao, cronograma));
    }

    [Fact(DisplayName = "O pedido de isenção depois da inscrição mantém a própria posição")]
    public void IsencaoDepoisDaInscricao_MantemAPosicao()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma isencao = FaseDeTeste.Criar(3, "ISENCAO", coletaSolicitacaoIsencao: true);

        MomentoDoEnvio.Da(isencao, [inscricao, isencao]).Should().Be(3);
    }

    [Fact(DisplayName = "Havendo mais de uma fase de inscrição, o pedido de isenção acompanha a de menor ordem")]
    public void MaisDeUmaFaseDeInscricao_VaiAPelaMenorOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma segundaInscricao = FaseDeTeste.Criar(6, "INSCRICAO_COMPLEMENTAR", coletaInscricao: true);
        FaseCronograma primeiraInscricao = FaseDeTeste.Criar(4, "INSCRICAO", coletaInscricao: true);

        MomentoDoEnvio.Da(isencao, [isencao, segundaInscricao, primeiraInscricao]).Should().Be(4);
    }

    [Fact(DisplayName = "Sem fase de inscrição, o pedido de isenção envia na própria posição")]
    public void SemFaseDeInscricao_EnviaNaPropriaPosicao()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);

        MomentoDoEnvio.Da(isencao, [isencao]).Should().Be(2);
    }

    [Fact(DisplayName = "A marca no mesmo momento da anterior não muda a vez do envio")]
    public void MarcaNoMesmoMomentoDaAnterior_NaoMudaAVezDoEnvio()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true, noMesmoMomentoDaAnterior: true);

        MomentoDoEnvio.Da(isencao, [isencao, inscricao]).Should().Be(2);
        MomentoDoEnvio.Da(inscricao, [isencao, inscricao]).Should().Be(2);
    }
}

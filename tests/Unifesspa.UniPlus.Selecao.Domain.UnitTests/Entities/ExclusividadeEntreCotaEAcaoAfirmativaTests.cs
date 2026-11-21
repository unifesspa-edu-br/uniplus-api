namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDasCotasFederaisNaConformidade;

/// <summary>
/// Quem concorre a cota da lei não concorre a ação afirmativa, salvo a reserva para pessoa com
/// deficiência, que se soma às cotas (UNI-REQ-0142 e UNI-REQ-0266).
/// </summary>
public sealed class ExclusividadeEntreCotaEAcaoAfirmativaTests
{
    private const string AcQuilombola = "AC_Q";

    private static readonly PredicadoDnf CotaDaLei = Exige((OpcaoPelasCotasDaLei.Fato, true));
    private static readonly PredicadoDnf CotaDaLeiParaPessoaComDeficiencia =
        Exige((OpcaoPelasCotasDaLei.Fato, true), ("PCD", true), ("CONCORRER_PCD", true));
    private static readonly PredicadoDnf ReservaParaPessoaComDeficiencia = Exige(("PCD", true), ("CONCORRER_PCD", true));
    private static readonly PredicadoDnf RegraAntigaDaReserva =
        Exige((OpcaoPelasCotasDaLei.Fato, false), ("PCD", true), ("CONCORRER_PCD", true));

    [Fact(DisplayName = "A reserva para pessoa com deficiência junto das cotas da lei deixa o item verde, e o processo publica")]
    public void ReservaParaPessoaComDeficienciaJuntoDasCotas_ItemVerdeEPublica()
    {
        ProcessoSeletivo processo = ProcessoComCotaEAcaoAfirmativa(CotaDaLei, ReservaParaPessoaComDeficiencia, ModalidadesFederaisLei12711.AcPcd);

        SoEstesItensVermelhos(processo);
        Publicar(processo).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "A regra da cota para pessoa com deficiência pode valer junto da AC_PCD, e junto de outra ação afirmativa não pode")]
    [InlineData(ModalidadesFederaisLei12711.AcPcd, true)]
    [InlineData(AcaoAfirmativaInstitucional, false)]
    public void CotaParaPessoaComDeficiencia_SoPodeValerJuntoDaAcPcd(string acaoAfirmativa, bool itemVerde) =>
        ItemDaExclusividadeOk(ProcessoComCotaEAcaoAfirmativa(CotaDaLeiParaPessoaComDeficiencia, ReservaParaPessoaComDeficiencia, acaoAfirmativa))
            .Should().Be(itemVerde);

    [Theory(DisplayName = "A ação afirmativa institucional que não recusa a opção pelas cotas é recusada junto das cotas, com o par no detalhe")]
    [InlineData(AcaoAfirmativaInstitucional)]
    [InlineData(AcQuilombola)]
    public void AcaoAfirmativaInstitucionalSemRecusarAOpcao_JuntoDasCotas_Recusa(string acaoAfirmativa)
    {
        ProcessoSeletivo processo = ProcessoComCotaEAcaoAfirmativa(CotaDaLei, Exige(("INDIGENA", true)), acaoAfirmativa);

        DomainError? recusa = ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(processo.Declaracao());

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.CotaEAcaoAfirmativaDerivaveisJuntas);
        recusa.Message.Should().Contain($"ação afirmativa \"{acaoAfirmativa}\"").And.Contain(OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas);
    }

    [Fact(DisplayName = "A ação afirmativa institucional junto da AC_PCD e das cotas: só a institucional é acusada")]
    public void AcaoAfirmativaInstitucionalJuntoDaAcPcd_SoAInstitucionalEAcusada()
    {
        ProcessoSeletivo processo = ProcessoComCotasEAcoesAfirmativas(
            CotaDaLei,
            (ModalidadesFederaisLei12711.AcPcd, ReservaParaPessoaComDeficiencia),
            (AcaoAfirmativaInstitucional, Exige(("INDIGENA", true))));

        DomainError? recusa = ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(processo.Declaracao());

        recusa!.Message.Should().Contain($"ação afirmativa \"{AcaoAfirmativaInstitucional}\"")
            .And.NotContain($"ação afirmativa \"{ModalidadesFederaisLei12711.AcPcd}\"");
    }

    [Fact(DisplayName = "A cópia antiga da AC_PCD, que recusa a opção pelas cotas, é recusada pela forma, e a exclusividade fica verde")]
    public void CopiaAntigaDaAcPcd_RecusadaPelaForma_ExclusividadeVerde()
    {
        ProcessoSeletivo processo = ProcessoComCotaEAcaoAfirmativa(CotaDaLei, RegraAntigaDaReserva, ModalidadesFederaisLei12711.AcPcd);

        DomainError? incoerente = RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(processo.Declaracao().Vagas.DistribuicaoVagas);

        incoerente!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
        incoerente.Message.Should().Contain(ModalidadesFederaisLei12711.AcPcd);
        ItemDaExclusividadeOk(processo).Should().BeTrue();
    }
}

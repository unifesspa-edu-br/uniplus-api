namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using static DistribuicaoDeVagasDeTeste;

/// <summary>A recusa da distribuição de vagas descreve a regra violada e não repete o que a pessoa enviou.</summary>
public sealed class ConferenciaDaDistribuicaoDeVagasSemValorRecebidoTests
{
    private const string ModalidadeFicticia = "MODALIDADE_FICTICIA_X";

    private static FieldError RecusaDe(
        DistribuicaoDeVagasDeclarada declarada, string codigoDoErro, int? teto = null, string[]? admitidas = null) =>
        ConferenciaDaDistribuicaoDeVagas.Conferir(declarada, teto, admitidas)
            .Should().ContainSingle(recusa => recusa.Error.Code == codigoDoErro).Subject;

    private static void NaoRepete(FieldError recusa, params string[] valoresRecebidos) =>
        new[] { recusa.Error.Message, recusa.Field }
            .Should().OnlyContain(texto => valoresRecebidos.All(valor => !texto!.Contains(valor, StringComparison.OrdinalIgnoreCase)));

    [Fact(DisplayName = "As vagas ofertadas base acima do teto não repetem o número recebido e mantêm o teto do cadastro")]
    public void AcimaDoTeto_NaoRepeteAsVagasRecebidas()
    {
        FieldError recusa = RecusaDe(
            DaLeiDeCotas() with { VagasOfertadasBase = 7919 },
            DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseAcimaDasVagasAutorizadas, teto: 49);

        NaoRepete(recusa, "7919");
        recusa.Error.Message.Should().Contain("49");
    }

    [Fact(DisplayName = "A modalidade fora do rol não cita código de modalidade, enviado ou do rol, nem o da regra")]
    public void ForaDoRol_NaoRepeteModalidadeNemRegra()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 25), Fixada(ModalidadeFicticia, 25));

        FieldError recusa = RecusaDe(declarada, DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra, admitidas: ["IND"]);

        NaoRepete(recusa, ModalidadeFicticia, declarada.CodigoDaRegra!, "IND");
    }

    [Fact(DisplayName = "A modalidade do rol ausente não repete o código da regra e mantém a modalidade do cadastro")]
    public void RolIncompleto_NaoRepeteARegra()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 50));

        FieldError recusa = RecusaDe(declarada, DistribuicaoDeVagasErrorCodes.ModalidadeDoRolAusente, admitidas: ["IND", "QUI"]);

        NaoRepete(recusa, declarada.CodigoDaRegra!);
        recusa.Error.Message.Should().Contain("QUI");
    }

    [Fact(DisplayName = "A retirada fora da ampla concorrência não repete o código da modalidade")]
    public void RetiradaForaDaAmplaConcorrencia_NaoRepeteAModalidade()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades.Add(Modalidade(
            ModalidadeFicticia, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, 2, ModalidadesFederaisLei12711.LbPpi));

        NaoRepete(
            RecusaDe(DaLeiDeCotas() with { Modalidades = modalidades }, DistribuicaoDeVagasErrorCodes.RetiradaFederalDeveSerDeAmplaConcorrencia),
            ModalidadeFicticia);
    }

    [Fact(DisplayName = "A modalidade calculada com quantidade fixada não repete o código da modalidade")]
    public void CalculadaComQuantidade_NaoRepeteAModalidade()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades[0] = Modalidade(
            ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, 3);

        NaoRepete(
            RecusaDe(DaLeiDeCotas() with { Modalidades = modalidades }, DistribuicaoDeVagasErrorCodes.QuantidadeCalculadaNaoInformavel),
            ModalidadesFederaisLei12711.LbPpi);
    }

    [Fact(DisplayName = "A retirada sem quantidade fixada não repete o código da modalidade")]
    public void RetiradaSemQuantidade_NaoRepeteAModalidade()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades.Add(Modalidade(
            ModalidadeFicticia, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, null, ModalidadesFederaisLei12711.Ac));

        NaoRepete(
            RecusaDe(DaLeiDeCotas() with { Modalidades = modalidades }, DistribuicaoDeVagasErrorCodes.QuantidadeDeclaradaObrigatoria),
            ModalidadeFicticia);
    }

    [Fact(DisplayName = "A modalidade do quadro fixado sem quantidade não repete o código da modalidade")]
    public void QuadroFixadoSemQuantidade_NaoRepeteAModalidade() =>
        NaoRepete(
            RecusaDe(
                FixadaNoEdital(Fixada("IND", 50), Modalidade(ModalidadeFicticia, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal)),
                DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente),
            ModalidadeFicticia);

    [Fact(DisplayName = "As oito federais faltantes são as da lei, não as enviadas, e continuam na mensagem")]
    public void FederaisFaltantes_CitamAsDaLei()
    {
        FieldError recusa = RecusaDe(
            DaLeiDeCotas() with { Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Skip(1)] },
            DistribuicaoDeVagasErrorCodes.ModalidadesFederaisIncompletas);

        recusa.Error.Message.Should().Contain(ModalidadesFederaisLei12711.Codigos[0]);
    }
}

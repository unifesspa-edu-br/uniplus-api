namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.CadastroDeReserva;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// As pessoas do cadastro de reserva que uma chamada convoca (UNI-REQ-0354): o máximo é o número de vagas da
/// chamada vezes as pessoas da reserva por vaga declaradas para ela, e a chamada sem declaração não convoca reserva.
/// </summary>
public sealed class PessoasDaReservaConvocadasTests
{
    private static NumeroDaChamada Numero(int valor) => NumeroDaChamada.Criar(valor).Value!;

    private static CadastroDeReservaPorChamada Declarada(params (int Chamada, int PessoasPorVaga)[] chamadas) =>
        CadastroDeReservaPorChamada.Declarar([.. chamadas.Select(static c => new CadastroDeReservaInformado(c.Chamada, c.PessoasPorVaga, c.PessoasPorVaga > 0 ? "ASSUME_NA_MESMA_CHAMADA" : null))]).Value!;

    [Fact(DisplayName = "A primeira chamada com duas pessoas da reserva por vaga e três vagas convoca seis")]
    public void PrimeiraChamada_DuasPorVaga_TresVagas_ConvocaSeis()
    {
        Declarada((1, 2), (2, 1)).PessoasDaReservaConvocadas(Numero(1), vagasDaChamada: 3).Should().Be(6);
    }

    [Fact(DisplayName = "A segunda chamada com uma pessoa da reserva por vaga e três vagas convoca três")]
    public void SegundaChamada_UmaPorVaga_TresVagas_ConvocaTres()
    {
        Declarada((1, 2), (2, 1)).PessoasDaReservaConvocadas(Numero(2), vagasDaChamada: 3).Should().Be(3);
    }

    [Fact(DisplayName = "A segunda chamada de Medicina, com duas vagas e duas pessoas da reserva por vaga, convoca quatro")]
    public void Medicina_SegundaChamada_DuasVagas_DuasPorVaga_ConvocaQuatro()
    {
        Declarada((2, 2)).PessoasDaReservaConvocadas(Numero(2), vagasDaChamada: 2).Should().Be(4);
    }

    [Fact(DisplayName = "A chamada sem declaração não convoca pessoa da reserva")]
    public void ChamadaSemDeclaracao_NaoConvocaReserva()
    {
        Declarada((2, 1)).PessoasDaReservaConvocadas(Numero(1), vagasDaChamada: 3).Should().Be(0);
    }

    [Fact(DisplayName = "O processo sem cadastro de reserva não convoca pessoa da reserva em chamada alguma")]
    public void SemReserva_NaoConvocaReserva()
    {
        CadastroDeReservaPorChamada.SemReserva.PessoasDaReservaConvocadas(Numero(1), vagasDaChamada: 3).Should().Be(0);
    }

    [Fact(DisplayName = "A chamada sem vaga não convoca pessoa da reserva")]
    public void ChamadaSemVaga_NaoConvocaReserva()
    {
        Declarada((1, 2)).PessoasDaReservaConvocadas(Numero(1), vagasDaChamada: 0).Should().Be(0);
    }

    [Fact(DisplayName = "As vagas negativas da chamada são recusadas")]
    public void VagasNegativas_Lancam()
    {
        Action convocar = () => Declarada((1, 2)).PessoasDaReservaConvocadas(Numero(1), vagasDaChamada: -1);

        convocar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O maior número de vagas com a maior quantidade por vaga não estoura")]
    public void MaiorVagaComMaiorQuantidade_NaoEstoura()
    {
        Declarada((1, int.MaxValue)).PessoasDaReservaConvocadas(Numero(1), int.MaxValue)
            .Should().Be((long)int.MaxValue * int.MaxValue);
    }
}

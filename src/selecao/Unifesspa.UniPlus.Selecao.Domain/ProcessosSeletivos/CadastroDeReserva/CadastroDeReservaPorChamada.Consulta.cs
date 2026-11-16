namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

public sealed partial record CadastroDeReservaPorChamada
{
    /// <summary>
    /// O máximo de pessoas do cadastro de reserva que a chamada convoca: as vagas da chamada vezes as pessoas da
    /// reserva por vaga declaradas para ela, ou zero na chamada sem declaração. As vagas vêm de quem convoca, e
    /// a declaração nunca lê o quadro. Reduzir o máximo às pessoas elegíveis é de quem convoca.
    /// </summary>
    /// <param name="chamada">A chamada que convoca.</param>
    /// <param name="vagasDaChamada">As vagas que a chamada tem, não negativas.</param>
    public long PessoasDaReservaConvocadas(NumeroDaChamada chamada, int vagasDaChamada)
    {
        ArgumentNullException.ThrowIfNull(chamada);
        ArgumentOutOfRangeException.ThrowIfNegative(vagasDaChamada);

        CadastroDeReservaDaChamada? declarada = Chamadas.FirstOrDefault(cadastro => cadastro.Chamada == chamada);
        return declarada is null ? 0 : (long)vagasDaChamada * declarada.PessoasDaReservaPorVaga;
    }
}

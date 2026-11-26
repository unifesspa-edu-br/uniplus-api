namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>Os códigos de recusa do cadastro de reserva por chamada declarado pelo processo (UNI-REQ-0354, UNI-REQ-0355).</summary>
public static class CadastroDeReservaErrorCodes
{
    /// <summary>A chamada do cadastro de reserva não informa o número, ou informa um número menor que 1.</summary>
    public const string NumeroDaChamadaInvalido = "CadastroDeReserva.NumeroDaChamadaInvalido";

    /// <summary>A chamada já tem cadastro de reserva declarado no processo.</summary>
    public const string ChamadaRepetida = "CadastroDeReserva.ChamadaRepetida";

    /// <summary>A chamada não informa a quantidade de pessoas da reserva por vaga, ou informa uma quantidade negativa.</summary>
    public const string PessoasDaReservaPorVagaInvalida = "CadastroDeReserva.PessoasDaReservaPorVagaInvalida";
}

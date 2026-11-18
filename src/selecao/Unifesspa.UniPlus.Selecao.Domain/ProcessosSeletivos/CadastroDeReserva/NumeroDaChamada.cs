namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O número de uma chamada do processo, um inteiro a partir de 1. Não tem teto: quantas chamadas o processo
/// faz é decisão do edital.
/// </summary>
public sealed record NumeroDaChamada
{
    private NumeroDaChamada(int valor) => Valor = valor;

    public int Valor { get; }

    public static Result<NumeroDaChamada> Criar(int? valor) =>
        valor is >= 1
            ? Result<NumeroDaChamada>.Success(new(valor.Value))
            : Result<NumeroDaChamada>.Failure(new DomainError(
                CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido,
                "Informe o número da chamada, um inteiro a partir de 1."));
}

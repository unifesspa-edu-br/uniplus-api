namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record NumeroEdital
{
    public int Numero { get; }
    public int Ano { get; }

    private NumeroEdital(int numero, int ano)
    {
        Numero = numero;
        Ano = ano;
    }

    public static Result<NumeroEdital> Criar(int numero, int ano)
    {
        if (numero <= 0)
            return Result<NumeroEdital>.Failure(new DomainError("NumeroEdital.Invalido", "Número do edital deve ser positivo."));

        if (ano < 2000 || ano > 2100)
            return Result<NumeroEdital>.Failure(new DomainError("NumeroEdital.AnoInvalido", "Ano do edital fora do intervalo válido."));

        return Result<NumeroEdital>.Success(new NumeroEdital(numero, ano));
    }

    public override string ToString() => $"{Numero:D3}/{Ano}";
}

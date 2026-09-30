namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

/// <summary>
/// Código de Endereçamento Postal: oito dígitos. Guarda só os dígitos; a máscara expõe a região
/// postal (os dois primeiros).
/// </summary>
public sealed record Cep
{
    public string Valor { get; }

    private Cep(string valor) => Valor = valor;

    public static Result<Cep> Criar(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
            return Result<Cep>.Failure(new DomainError("Cep.Vazio", "CEP é obrigatório."));

        // Só dígitos e os separadores usuais de CEP; qualquer outro caractere recusa.
        if (!cep.All(static c => char.IsAsciiDigit(c) || c is ' ' or '-' or '.'))
            return Result<Cep>.Failure(new DomainError("Cep.Invalido", "CEP inválido: informe os oito dígitos."));

        string digitos = new([.. cep.Where(char.IsAsciiDigit)]);
        if (digitos.Length != 8 || digitos.Distinct().Count() == 1)
            return Result<Cep>.Failure(new DomainError("Cep.Invalido", "CEP inválido: informe os oito dígitos."));

        return Result<Cep>.Success(new Cep(digitos));
    }

    /// <summary>A região postal, como em <c>68***-***</c>.</summary>
    public string Mascarado => $"{Valor[..2]}***-***";

    public override string ToString() => Mascarado;
}

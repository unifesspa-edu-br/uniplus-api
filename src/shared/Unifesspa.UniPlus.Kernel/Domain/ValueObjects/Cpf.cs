namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

public sealed record Cpf
{
    /// <summary>O tamanho do CPF mascarado, <c>***.999.999-**</c>.</summary>
    public const int TamanhoMascarado = 14;

    public string Valor { get; }

    private Cpf(string valor) => Valor = valor;

    public static Result<Cpf> Criar(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return Result<Cpf>.Failure(new DomainError("Cpf.Vazio", "CPF é obrigatório."));

        // Só dígitos e os separadores usuais de CPF; qualquer outro caractere recusa.
        if (!cpf.All(static c => char.IsAsciiDigit(c) || c is ' ' or '.' or '-'))
            return Result<Cpf>.Failure(new DomainError("Cpf.Invalido", "CPF inválido."));

        string apenasDigitos = new(cpf.Where(char.IsAsciiDigit).ToArray());

        if (apenasDigitos.Length != 11 || !ValidarDigitos(apenasDigitos))
            return Result<Cpf>.Failure(new DomainError("Cpf.Invalido", "CPF inválido."));

        return Result<Cpf>.Success(new Cpf(apenasDigitos));
    }

    public string Mascarado => Mascarar(Valor);

    /// <summary>Os onze dígitos com a pontuação usual, <c>999.999.999-99</c>.</summary>
    public string Formatado => $"{Valor[..3]}.{Valor[3..6]}.{Valor[6..9]}-{Valor[9..]}";

    /// <summary>Mascara onze dígitos como <c>***.999.999-**</c>, sem exigir dígito verificador válido.</summary>
    public static string Mascarar(string onzeDigitos)
    {
        ArgumentNullException.ThrowIfNull(onzeDigitos);
        ArgumentOutOfRangeException.ThrowIfNotEqual(onzeDigitos.Length, 11);

        return $"***.{onzeDigitos[3..6]}.{onzeDigitos[6..9]}-**";
    }

    public override string ToString() => Mascarado;

    private static bool ValidarDigitos(string cpf)
    {
        if (cpf.Distinct().Count() == 1) return false;

        int[] multiplicadores1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] multiplicadores2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

        string tempCpf = cpf[..9];
        int soma = tempCpf.Select((c, i) => (c - '0') * multiplicadores1[i]).Sum();
        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        tempCpf += digito1;
        soma = tempCpf.Select((c, i) => (c - '0') * multiplicadores2[i]).Sum();
        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return cpf.EndsWith($"{digito1}{digito2}", StringComparison.Ordinal);
    }
}

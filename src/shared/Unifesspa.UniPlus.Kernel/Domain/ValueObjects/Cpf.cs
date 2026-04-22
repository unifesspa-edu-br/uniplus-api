namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record Cpf
{
    public string Valor { get; }

    private Cpf(string valor) => Valor = valor;

    public static Result<Cpf> Criar(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return Result<Cpf>.Failure(new DomainError("Cpf.Vazio", "CPF é obrigatório."));

        string apenasDigitos = new(cpf.Where(char.IsDigit).ToArray());

        if (apenasDigitos.Length != 11 || !ValidarDigitos(apenasDigitos))
            return Result<Cpf>.Failure(new DomainError("Cpf.Invalido", "CPF inválido."));

        return Result<Cpf>.Success(new Cpf(apenasDigitos));
    }

    public string Mascarado => $"***.***.***-{Valor[9..]}";

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

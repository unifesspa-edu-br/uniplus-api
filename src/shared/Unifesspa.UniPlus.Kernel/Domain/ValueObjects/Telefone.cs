namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

/// <summary>
/// Telefone brasileiro com DDD: dez dígitos (fixo) ou onze (celular, iniciado por 9 depois do DDD).
/// Guarda só os dígitos; a máscara expõe o DDD e os quatro últimos dígitos.
/// </summary>
public sealed record Telefone
{
    public string Valor { get; }

    private Telefone(string valor) => Valor = valor;

    public static Result<Telefone> Criar(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return Result<Telefone>.Failure(new DomainError("Telefone.Vazio", "Telefone é obrigatório."));

        // Só dígitos e os separadores usuais de telefone; qualquer outro caractere recusa, em vez
        // de ser descartado em silêncio.
        if (!telefone.All(static c => char.IsAsciiDigit(c) || c is ' ' or '(' or ')' or '-' or '.'))
            return Result<Telefone>.Failure(new DomainError("Telefone.Invalido", "Telefone inválido: informe o DDD e o número."));

        string digitos = new([.. telefone.Where(char.IsAsciiDigit)]);
        bool fixo = digitos.Length == 10;
        bool celular = digitos.Length == 11 && digitos[2] == '9';
        bool dddValido = digitos.Length >= 2 && digitos[0] != '0' && digitos[1] != '0';

        if (!(fixo || celular) || !dddValido)
            return Result<Telefone>.Failure(new DomainError("Telefone.Invalido", "Telefone inválido: informe o DDD e o número."));

        return Result<Telefone>.Success(new Telefone(digitos));
    }

    /// <summary>O DDD e os quatro últimos dígitos, como em <c>(94) *****-1234</c>.</summary>
    public string Mascarado => $"({Valor[..2]}) {new string('*', Valor.Length - 6)}-{Valor[^4..]}";

    public override string ToString() => Mascarado;
}

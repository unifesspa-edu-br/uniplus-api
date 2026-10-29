namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Identidade de uma pessoa como o token a traz: emissor e <c>sub</c>, ambos opacos, e o nome de exibição de quem age.
/// O nome só informa quem lê a trilha: não identifica a pessoa, e por isso fica fora da igualdade. Nunca carrega CPF.
/// </summary>
public sealed record Identidade
{
    public string Emissor { get; }

    public string Sub { get; }

    /// <summary>Nome social quando a pessoa tem, senão o nome civil; nulo quando o token não traz nenhum.</summary>
    public string? Nome { get; }

    public Identidade(string emissor, string sub, string? nome = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emissor);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        Emissor = emissor;
        Sub = sub;
        Nome = nome;
    }

    public bool Equals(Identidade? other) =>
        other is not null
        && string.Equals(Emissor, other.Emissor, StringComparison.Ordinal)
        && string.Equals(Sub, other.Sub, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Emissor, Sub);
}

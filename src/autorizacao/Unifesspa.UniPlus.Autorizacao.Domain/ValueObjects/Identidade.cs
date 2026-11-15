namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Identidade de uma pessoa como o token a traz: emissor e <c>contaId</c>, ambos opacos, e o nome de exibição de quem age.
/// O nome só informa quem lê a trilha: não identifica a pessoa, e por isso fica fora da igualdade. Nunca carrega CPF.
/// </summary>
public sealed record Identidade
{
    public string Emissor { get; }

    public string ContaId { get; }

    /// <summary>Nome social quando a pessoa tem, senão o nome civil; nulo quando o token não traz nenhum.</summary>
    public string? Nome { get; }

    public Identidade(string emissor, string contaId, string? nome = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emissor);
        ArgumentException.ThrowIfNullOrWhiteSpace(contaId);

        Emissor = emissor;
        ContaId = contaId;
        Nome = nome;
    }

    public bool Equals(Identidade? other) =>
        other is not null
        && string.Equals(Emissor, other.Emissor, StringComparison.Ordinal)
        && string.Equals(ContaId, other.ContaId, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Emissor, ContaId);
}

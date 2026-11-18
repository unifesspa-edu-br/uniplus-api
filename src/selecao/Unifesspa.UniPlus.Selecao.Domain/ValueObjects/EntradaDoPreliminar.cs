namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que entra no resultado preliminar que uma fase ou uma etapa publica: a regra e os dados dos quais o
/// preliminar depende (UNI-REQ-0157, UNI-REQ-0282). Duas entradas são iguais quando trazem as mesmas linhas.
/// </summary>
/// <remarks>
/// Cada linha é <c>campo=valor</c>, escrita em forma canônica: texto em NFC, números com ponto decimal e sem
/// zeros à direita, instantes em UTC no formato <c>O</c>, e coleções identificadas pelo que as distingue (origens,
/// códigos, tipo de ato), nunca pelo <c>Id</c> da linha, que muda quando a redefinição do cronograma recria a filha.
/// As linhas ficam em ordem ordinal, de modo que a ordem em que a entidade as produz não conta.
/// </remarks>
public sealed record EntradaDoPreliminar
{
    public EntradaDoPreliminar(IEnumerable<string> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);

        Linhas = [.. linhas.Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> Linhas { get; }

    public bool Equals(EntradaDoPreliminar? other) =>
        other is not null && Linhas.SequenceEqual(other.Linhas, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        HashCode codigo = new();
        foreach (string linha in Linhas)
        {
            codigo.Add(linha, StringComparer.Ordinal);
        }

        return codigo.ToHashCode();
    }
}

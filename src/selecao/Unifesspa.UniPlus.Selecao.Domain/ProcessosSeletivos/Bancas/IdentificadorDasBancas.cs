namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O identificador que a declaração dá a uma banca para dizer qual banca já gravada ela substitui. Cada banca de uma fase ou de
/// uma etapa tem identificador próprio: o mesmo identificador em duas bancas faria as duas disputarem a mesma banca gravada.
/// </summary>
public static class IdentificadorDasBancas
{
    /// <summary>Quem fala: a fase ou a etapa que declara as bancas, no texto da recusa.</summary>
    public const string DaFase = "fase";

    /// <inheritdoc cref="DaFase"/>
    public const string DaEtapa = "etapa";

    /// <summary>
    /// Uma recusa por banca, da segunda ocorrência em diante, cujo identificador já apareceu; a banca sem identificador não
    /// conta. O campo é <c>{lista}[posição].id</c>.
    /// </summary>
    public static IReadOnlyList<FieldError> Repetidos(IEnumerable<Guid?> identificadores, string lista, string lugar)
    {
        ArgumentNullException.ThrowIfNull(identificadores);

        HashSet<Guid> vistos = [];
        return [.. identificadores
            .Select(static (id, posicao) => (id, posicao))
            .Where(par => par.id.HasValue && !vistos.Add(par.id.Value))
            .Select(par => new FieldError($"{lista}[{par.posicao}].id", Recusa(lugar)))];
    }

    internal static IReadOnlyList<FieldError> Repetidos<TBanca>(IReadOnlyList<TBanca> declaradas, string lista, string lugar)
        where TBanca : class, IBancaDoLugar<TBanca> =>
        Repetidos(declaradas.Select(static banca => banca.IdDaBancaQueSubstitui), lista, lugar);

    private static DomainError Recusa(string lugar) => new(
        BancaErrorCodes.IdDuplicado,
        $"O mesmo identificador de banca foi informado em mais de uma banca da {lugar}; cada banca tem identificador próprio.");
}

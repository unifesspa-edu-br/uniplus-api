namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Forma canônica de um parâmetro de consulta que recorta a listagem. Recebe o valor efetivo, o mesmo que vai ao
/// <c>WHERE</c>, depois do valor padrão e da normalização que o filtro já faz: dois pedidos que recortam o mesmo
/// conjunto dão a mesma parte.
/// </summary>
public sealed record ParteDoFiltro
{
    private const string MarcaDeAusente = "~";
    private const string MarcaDeValor = "=";
    private const string MarcaDeConjunto = "[";

    private ParteDoFiltro(string forma) => Forma = forma;

    /// <summary>O filtro não recorta.</summary>
    public static ParteDoFiltro Ausente { get; } = new(MarcaDeAusente);

    internal string Forma { get; }

    /// <summary>Texto informado; o texto vazio é um valor, diferente de ausente.</summary>
    public static ParteDoFiltro De(string? valor) =>
        valor is null ? Ausente : new(MarcaDeValor + valor);

    public static ParteDoFiltro De(Guid? valor) =>
        De(valor?.ToString("D", CultureInfo.InvariantCulture));

    public static ParteDoFiltro De(bool? valor) =>
        De(valor is { } v ? (v ? "true" : "false") : null);

    public static ParteDoFiltro De(DateOnly? valor) =>
        De(valor?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static ParteDoFiltro De(DateTimeOffset? valor) =>
        De(valor is { } v ? InstanteDoCursor.Escrever(v) : null);

    /// <summary>Enumeração pelo código de contrato, nunca pelo nome do membro.</summary>
    public static ParteDoFiltro De<TEnum>(TEnum? valor)
        where TEnum : struct, Enum =>
        De(valor?.Codigo());

    /// <summary>
    /// Conjunto de valores em ordem ordinal e sem repetição: coleção reordenada ou repetida dá a mesma parte, e a
    /// coleção vazia que não recorta é o conjunto vazio.
    /// </summary>
    public static ParteDoFiltro Conjunto(IEnumerable<string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        string[] canonicos = [.. valores.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return new(MarcaDeConjunto + CompositeSortKey.Serialize(canonicos));
    }

    /// <summary>Conjunto de enumerações pelos códigos de contrato.</summary>
    public static ParteDoFiltro Conjunto<TEnum>(IEnumerable<TEnum> valores)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(valores);

        return Conjunto(valores.Select(static v => v.Codigo()));
    }

    /// <summary>Conjunto de identificadores no formato <c>D</c>.</summary>
    public static ParteDoFiltro Conjunto(IEnumerable<Guid> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        return Conjunto(valores.Select(static v => v.ToString("D", CultureInfo.InvariantCulture)));
    }
}

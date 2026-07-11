namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

/// <summary>
/// Compõe a etiqueta de recurso que viaja dentro do cursor opaco
/// (<see cref="CursorPayload.ResourceTag"/>, ADR-0026) — o campo que impede um cursor
/// emitido num recurso de ser navegado noutro.
/// </summary>
/// <remarks>
/// <para>
/// Na maioria dos recursos a etiqueta é uma constante (<c>"atos"</c>, <c>"editais"</c>).
/// Ela deixa de bastar quando a coleção é <b>escopada por um segmento da rota</b>: os
/// atos de um certame e os de outro são coleções distintas sob a mesma etiqueta, e um
/// cursor emitido numa retomaria a paginação da outra — na âncora errada, com resultados
/// que o cliente não pediu. Acrescentar os valores de rota ao escopo fecha isso.
/// </para>
/// <para>
/// <b>Um só compositor, para os dois lados.</b> A etiqueta é montada na emissão (pelo
/// controller) e conferida na leitura (pelo binder). Se cada lado a montasse à sua
/// maneira, uma diferença de separador, de ordem ou de grafia invalidaria todos os
/// cursores legítimos — e o sintoma seria um 400 inexplicável, não um erro de compilação.
/// </para>
/// <para>
/// A normalização existe porque o binder lê o segmento <b>cru</b> da URL, e o controller,
/// o valor já tipado: <c>…/8B3E…/atos</c> e <c>…/8b3e…/atos</c> designam a mesma entidade,
/// e têm de produzir a mesma etiqueta. Um valor que seja um GUID é reduzido à forma
/// canônica; os demais vão como estão, apenas sem espaços nas pontas.
/// </para>
/// </remarks>
public static class CursorResourceTag
{
    private const char Separador = ':';

    /// <summary>
    /// Devolve <paramref name="resource"/> quando não há valores de escopo, ou
    /// <c>recurso:valor1:valor2</c> na ordem dada.
    /// </summary>
    public static string Compose(string resource, IEnumerable<string?> valoresDeEscopo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentNullException.ThrowIfNull(valoresDeEscopo);

        string[] normalizados = [.. valoresDeEscopo.Select(Normalizar)];

        return normalizados.Length == 0
            ? resource
            : string.Join(Separador, [resource, .. normalizados]);
    }

    private static string Normalizar(string? valor)
    {
        string bruto = valor?.Trim() ?? string.Empty;

        return Guid.TryParse(bruto, CultureInfo.InvariantCulture, out Guid id)
            ? id.ToString("D", CultureInfo.InvariantCulture)
            : bruto;
    }
}

namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O identificador de um item de lista no <c>errors[]</c> da ADR-0023, com os nomes no fio fixos pelos atributos.
/// </summary>
/// <param name="Lista">O nome da lista que o campo percorre, como aparece no caminho (<c>fases</c>, <c>etapas</c>, <c>recursos</c>).</param>
/// <param name="Identificador">O identificador estável do item na lista: um código de catálogo ou um identificador opaco.</param>
public sealed record IdentificadorDoItemNoFio(
    [property: JsonPropertyName("lista")] string Lista,
    [property: JsonPropertyName("identificador")] string Identificador)
{
    public static IdentificadorDoItemNoFio De(IdentificadorDoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new IdentificadorDoItemNoFio(item.Lista, item.Identificador);
    }
}

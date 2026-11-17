namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Declara no contrato o corpo <c>application/json</c> de uma action que lê o corpo à mão, sem parâmetro de binding: o MVC
/// só descreve o corpo a partir de um parâmetro <c>[FromBody]</c>. O formato do documento é o do JSON Schema publicado, que o
/// contrato apenas referencia: o esquema é a fonte, e a especificação não o duplica.
/// </summary>
/// <param name="esquema">O <c>$id</c> do JSON Schema que descreve o corpo.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CorpoJsonAttribute(string esquema) : Attribute
{
    public string Esquema { get; } = esquema;
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Declara no contrato o corpo <c>application/json</c> de uma action que lê o corpo à mão, sem parâmetro de binding: o MVC
/// só descreve o corpo a partir de um parâmetro <c>[FromBody]</c>. O formato do corpo é o de um componente do documento
/// OpenAPI, que o módulo publica a partir do JSON Schema; o contrato referencia o componente, e o cliente gerado o tipa.
/// </summary>
/// <param name="componente">O nome do componente (<c>components/schemas</c>) que descreve o corpo.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CorpoJsonAttribute(string componente) : Attribute
{
    public string Componente { get; } = componente;
}

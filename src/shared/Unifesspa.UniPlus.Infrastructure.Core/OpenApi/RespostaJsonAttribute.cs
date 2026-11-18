namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Declara no contrato o corpo <c>200</c> de uma action que devolve o documento sem tipo de retorno que o gerador conheça (um
/// <c>JsonObject</c>, por exemplo, só viraria um objeto sem propriedades). O formato do corpo é o de um componente do
/// documento OpenAPI, que o módulo publica a partir do JSON Schema.
/// </summary>
/// <param name="componente">O nome do componente (<c>components/schemas</c>) que descreve o corpo.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RespostaJsonAttribute(string componente) : Attribute
{
    public string Componente { get; } = componente;
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Publica a propriedade <c>codigo</c> de <see cref="CodigoComRotulo{TEnum}"/> como texto, com a lista dos códigos que o
/// enum declarou e a explicação de cada um, e dá ao componente a descrição do enum como um todo. Sem isto o gerador, que
/// não conhece o catálogo, deixaria o campo como texto livre.
/// </summary>
public sealed class CodigoComRotuloSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string NomeDaPropriedade = "codigo";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (EnumDoCodigoComRotulo(context.JsonTypeInfo.Type) is { } enumDoObjeto)
        {
            schema.Description = EnumsDeContrato.Descricao(enumDoObjeto);
        }
        else if (EnumDaPropriedadeCodigo(context) is { } enumDoCodigo)
        {
            PublicarCodigos(schema, EnumsDeContrato.Declarados(enumDoCodigo));
        }

        return Task.CompletedTask;
    }

    private static Type? EnumDaPropriedadeCodigo(OpenApiSchemaTransformerContext context) =>
        context.JsonPropertyInfo is { } propriedade && string.Equals(propriedade.Name, NomeDaPropriedade, StringComparison.Ordinal)
            ? EnumDoCodigoComRotulo(propriedade.DeclaringType)
            : null;

    private static Type? EnumDoCodigoComRotulo(Type tipo) =>
        tipo is { IsGenericType: true } && tipo.GetGenericTypeDefinition() == typeof(CodigoComRotulo<>)
            ? tipo.GetGenericArguments()[0]
            : null;

    private static void PublicarCodigos(OpenApiSchema schema, IReadOnlyList<CodigoDeclarado> declarados)
    {
        schema.Type = JsonSchemaType.String;
        schema.Enum = [.. declarados.Select(static d => (JsonNode)JsonValue.Create(d.Codigo))];
        schema.Description = string.Join('\n', declarados.Select(Linha));
    }

    private static string Linha(CodigoDeclarado declarado) =>
        declarado.Descricao is null
            ? $"`{declarado.Codigo}`: {declarado.Rotulo}"
            : $"`{declarado.Codigo}`: {declarado.Rotulo} — {declarado.Descricao}";
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Schema transformer que aplica invariantes de domínio Uni+ a propriedades
/// tipadas. Hoje cobre <c>cpf</c> (regex de 11 dígitos + nota PII) e
/// <c>valoresSelecionaveis</c> (cardinalidade mínima 1, issue #1077).
/// <para>
/// O <c>code</c> de ProblemDetails NÃO é coberto aqui: o campo vive em
/// <c>ProblemDetails.Extensions["code"]</c> (<c>[JsonExtensionData]</c>) e
/// não recebe um <c>JsonPropertyInfo</c> nomeado. Propriedade que chega por
/// extensão se declara sobre o documento já emitido, em
/// <see cref="ProblemDetailsExtensionsDocumentTransformer"/>.
/// </para>
/// </summary>
public sealed class UniPlusSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        // Enum-as-string sem `type` explícito: o ASP.NET Core OpenAPI generator
        // emite `enum: [...]` quando JsonStringEnumConverter está registrado,
        // mas não popula `type: "string"`. Sem o type, geradores de cliente
        // (openapi-typescript, openapi-generator, NSwag) podem tratar a schema
        // como `any`, perdendo type safety. Inferimos string sempre que houver
        // enum sem type — invariante OpenAPI 3.1.
        if (schema.Enum is { Count: > 0 } && !schema.Type.HasValue)
        {
            schema.Type = JsonSchemaType.String;
        }

        AjustarRequiredPelaSerializacao(schema, context);

        string? propertyName = context.JsonPropertyInfo?.Name;
        if (propertyName is null)
            return Task.CompletedTask;

        AplicarVocabularioFechado(schema, context);
        AplicarTokenDeClassificacao(schema, context);

        // JsonSchemaType é [Flags] — propriedades nullable saem com o flag Null somado ao
        // tipo base (ex.: Array | Null, String | Null), então comparação por igualdade exata
        // pula schemas legítimos. HasFlag pega o tipo base com ou sem nulidade.
        if (schema.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Array)
            && string.Equals(propertyName, "valoresSelecionaveis", StringComparison.Ordinal))
        {
            // issue #1077: um seletor de SELECAO_UNICA/SELECAO_MULTIPLA publicado sem opção
            // nenhuma não é respondível — o contrato exige pelo menos um valor selecionável
            // quando o array está presente (null continua válido para BOOLEANO/NUMERO).
            schema.MinItems = 1;
        }

        if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.String))
            return Task.CompletedTask;

        if (string.Equals(propertyName, "cpf", StringComparison.Ordinal))
        {
            schema.Pattern = @"^\d{11}$";
            schema.Description ??= "CPF (apenas dígitos, sem formatação). PII — sempre mascarar em logs ('***.999.999-**', ADR-0011).";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A propriedade que a serialização pode omitir não pode constar em <c>required</c>: a resposta projetada a omite de quem
    /// não pode vê-la, e o cliente gerado a tomaria por sempre presente. Quem decide o que se omite é
    /// <c>OmissaoDeCampoClassificado.PodeOmitir</c>, a mesma regra do modificador de serialização. A propriedade que sempre sai segue como
    /// o gerador a declarou; a que carrega <see cref="TokenDeClassificacaoAttribute"/> passa a obrigatória.
    /// </summary>
    private static void AjustarRequiredPelaSerializacao(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        // O pipeline chama o transformer do objeto aninhado com o JsonPropertyInfo da propriedade que o alcança, e o
        // JsonTypeInfo é sempre o do tipo do schema; por isso o ajuste não depende de como o schema foi alcançado.
        if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo propriedade in context.JsonTypeInfo.Properties)
        {
            if (TemTokenDeClassificacao(propriedade))
            {
                (schema.Required ??= new HashSet<string>(StringComparer.Ordinal)).Add(propriedade.Name);
            }
            else if (OmissaoDeCampoClassificado.PodeOmitir(propriedade))
            {
                schema.Required?.Remove(propriedade.Name);
            }
        }

        if (schema.Required is { Count: 0 })
        {
            schema.Required = null;
        }
    }

    private static bool TemTokenDeClassificacao(JsonPropertyInfo propriedade) =>
        propriedade.AttributeProvider is MemberInfo membro
        && Attribute.IsDefined(membro, typeof(TokenDeClassificacaoAttribute), inherit: true);

    /// <summary>Publica o token de classificação como texto não anulável, com o vocabulário fechado dos cinco valores.</summary>
    private static void AplicarTokenDeClassificacao(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo is not { } propriedade || !TemTokenDeClassificacao(propriedade))
        {
            return;
        }

        schema.Type = JsonSchemaType.String;
        schema.Enum = [.. ClassificacoesDeProtecao.TokensCanonicos.Select(static t => (JsonNode)JsonValue.Create(t)!)];
    }

    /// <summary>
    /// Publica como <c>enum</c> o vocabulário que a propriedade declara via
    /// <see cref="VocabularioFechadoAttribute"/>. Num array, o conjunto pertence ao ITEM,
    /// não à lista: aplicá-lo à lista diria que a lista inteira é um dos valores.
    /// </summary>
    private static void AplicarVocabularioFechado(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo?.AttributeProvider
                ?.GetCustomAttributes(typeof(VocabularioFechadoAttribute), inherit: true)
                .FirstOrDefault() is not VocabularioFechadoAttribute vocabulario
            || vocabulario.Valores.Count == 0)
        {
            return;
        }

        // JsonSchemaType é [Flags]: uma propriedade anulável sai com Null somado ao tipo
        // base, então a comparação precisa ser por flag e não por igualdade.
        bool ehArray = schema.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Array);
        IOpenApiSchema? alvo = ehArray ? schema.Items : schema;

        if (alvo is not OpenApiSchema destino)
        {
            return;
        }

        destino.Type ??= JsonSchemaType.String;
        destino.Enum = [.. vocabulario.Valores.Select(static v => (JsonNode)JsonValue.Create(v)!)];
    }
}

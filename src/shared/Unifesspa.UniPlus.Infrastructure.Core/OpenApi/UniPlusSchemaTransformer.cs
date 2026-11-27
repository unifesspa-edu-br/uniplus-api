namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Schema transformer que aplica invariantes de domínio Uni+ a propriedades
/// tipadas. Hoje cobre <c>cpf</c> (regex de 11 dígitos + nota PII) e
/// <c>valoresSelecionaveis</c> (cardinalidade mínima 1).
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

        AplicarInvariantes(schema, context);
        return Task.CompletedTask;
    }

    private static void AplicarInvariantes(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        InferirTipoDoEnum(schema);
        AjustarRequiredPelaSerializacao(schema, context);
        if (context.JsonPropertyInfo?.Name is { } propertyName)
        {
            AplicarVocabularioFechado(schema, context);
            AplicarTokenDeClassificacao(schema, context);
            AplicarObrigatorioNoContrato(schema, context);
            AplicarFormatoDoCodigo(schema, context);
            AplicarInvariantesDaPropriedade(schema, propertyName);
        }
    }

    // Enum-as-string sem `type` explícito: o ASP.NET Core OpenAPI generator
    // emite `enum: [...]` quando JsonStringEnumConverter está registrado,
    // mas não popula `type: "string"`. Sem o type, geradores de cliente
    // (openapi-typescript, openapi-generator, NSwag) podem tratar a schema
    // como `any`, perdendo type safety. Inferimos string sempre que houver
    // enum sem type — invariante OpenAPI 3.1.
    private static void InferirTipoDoEnum(OpenApiSchema schema)
    {
        if (schema.Enum is { Count: > 0 } && !schema.Type.HasValue)
        {
            schema.Type = JsonSchemaType.String;
        }
    }

    // JsonSchemaType é [Flags] — propriedades nullable saem com o flag Null somado ao
    // tipo base (ex.: Array | Null, String | Null), então comparação por igualdade exata
    // pula schemas legítimos. HasFlag pega o tipo base com ou sem nulidade.
    private static void AplicarInvariantesDaPropriedade(OpenApiSchema schema, string propertyName)
    {
        if (schema.Type is { } tipo)
        {
            ExigirAoMenosUmValorSelecionavel(schema, tipo, propertyName);
            DescreverCpf(schema, tipo, propertyName);
        }
    }

    // Um seletor de ESCOLHA_UNICA/ESCOLHA_MULTIPLA publicado sem opção
    // nenhuma não é respondível — o contrato exige pelo menos um valor selecionável
    // quando o array está presente (null continua válido para SIM_OU_NAO/NUMERO).
    private static void ExigirAoMenosUmValorSelecionavel(OpenApiSchema schema, JsonSchemaType tipo, string propertyName)
    {
        if (tipo.HasFlag(JsonSchemaType.Array) && string.Equals(propertyName, "valoresSelecionaveis", StringComparison.Ordinal))
        {
            schema.MinItems = 1;
        }
    }

    private static void DescreverCpf(OpenApiSchema schema, JsonSchemaType tipo, string propertyName)
    {
        if (tipo.HasFlag(JsonSchemaType.String) && string.Equals(propertyName, "cpf", StringComparison.Ordinal))
        {
            schema.Pattern = @"^\d{11}$";
            schema.Description ??= "CPF (apenas dígitos, sem formatação). PII — sempre mascarar em logs ('***.999.999-**', ADR-0011).";
        }
    }

    /// <summary>
    /// A propriedade que a serialização pode omitir não pode constar em <c>required</c>: a resposta projetada a omite de quem
    /// não pode vê-la, e o cliente gerado a tomaria por sempre presente. Quem decide o que se omite é
    /// <c>OmissaoDeCampoClassificado.PodeOmitir</c>, a mesma regra do modificador de serialização. A propriedade que sempre sai segue como
    /// o gerador a declarou; a que carrega <see cref="TokenDeClassificacaoAttribute"/> passa a obrigatória.
    /// </summary>
    // O pipeline chama o transformer do objeto aninhado com o JsonPropertyInfo da propriedade que o alcança, e o
    // JsonTypeInfo é sempre o do tipo do schema; por isso o ajuste não depende de como o schema foi alcançado.
    private static void AjustarRequiredPelaSerializacao(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo propriedade in context.JsonTypeInfo.Properties)
        {
            AjustarRequiredDaPropriedade(schema, propriedade);
        }

        schema.Required = schema.Required is { Count: 0 } ? null : schema.Required;
    }

    private static void AjustarRequiredDaPropriedade(OpenApiSchema schema, JsonPropertyInfo propriedade)
    {
        if (TemTokenDeClassificacao(propriedade) || TemAtributo<ObrigatorioNoContratoAttribute>(propriedade))
        {
            (schema.Required ??= new HashSet<string>(StringComparer.Ordinal)).Add(propriedade.Name);
        }
        else if (OmissaoDeCampoClassificado.PodeOmitir(propriedade))
        {
            schema.Required?.Remove(propriedade.Name);
        }
    }

    private static bool TemTokenDeClassificacao(JsonPropertyInfo propriedade) =>
        TemAtributo<TokenDeClassificacaoAttribute>(propriedade);

    private static bool TemAtributo<TAtributo>(JsonPropertyInfo propriedade)
        where TAtributo : Attribute =>
        propriedade.AttributeProvider is MemberInfo membro
        && Attribute.IsDefined(membro, typeof(TAtributo), inherit: true);

    /// <summary>Publica o campo marcado sem aceitar nulo, mantendo o tipo; a obrigatoriedade entra em <c>required</c> no objeto que o contém.</summary>
    private static void AplicarObrigatorioNoContrato(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo is { } propriedade && TemAtributo<ObrigatorioNoContratoAttribute>(propriedade))
        {
            schema.Type &= ~JsonSchemaType.Null;
        }
    }

    /// <summary>Publica o padrão e o limite do código do campo de texto marcado com <see cref="NoFormatoDoCodigoAttribute"/>.</summary>
    /// <exception cref="InvalidOperationException">O campo não é texto, ou também aceita códigos de um enum.</exception>
    private static void AplicarFormatoDoCodigo(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo is not { } propriedade || FormatoDoCodigoDe(propriedade) is not { } formato)
        {
            return;
        }

        if (propriedade.PropertyType != typeof(string) || CampoQueAceitaCodigos.De(propriedade) is not null)
        {
            throw new InvalidOperationException(
                $"O campo {propriedade.DeclaringType.Name}.{propriedade.Name} declara NoFormatoDoCodigo, que vale só para texto que não aceita códigos de um enum.");
        }

        schema.Pattern = FormatoDoCodigo.ExpressaoNoBanco;
        schema.MaxLength = formato.Maximo;
    }

    private static NoFormatoDoCodigoAttribute? FormatoDoCodigoDe(JsonPropertyInfo propriedade) =>
        propriedade.AttributeProvider?.GetCustomAttributes(typeof(NoFormatoDoCodigoAttribute), inherit: true).FirstOrDefault()
            as NoFormatoDoCodigoAttribute;

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
        if (VocabularioDaPropriedade(context) is { } vocabulario && AlvoDoVocabulario(schema) is { } destino)
        {
            destino.Type ??= JsonSchemaType.String;
            destino.Enum = [.. vocabulario.Valores.Select(static v => (JsonNode)JsonValue.Create(v)!)];
        }
    }

    private static VocabularioFechadoAttribute? VocabularioDaPropriedade(OpenApiSchemaTransformerContext context) =>
        context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(VocabularioFechadoAttribute), inherit: true).FirstOrDefault()
            is VocabularioFechadoAttribute { Valores.Count: > 0 } vocabulario
            ? vocabulario
            : null;

    // JsonSchemaType é [Flags]: uma propriedade anulável sai com Null somado ao tipo
    // base, então a comparação precisa ser por flag e não por igualdade.
    private static OpenApiSchema? AlvoDoVocabulario(OpenApiSchema schema) =>
        (schema.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Array) ? schema.Items : schema) as OpenApiSchema;
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Errors;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no schema <c>ProblemDetails</c> as propriedades que a resposta carrega
/// por <c>ProblemDetails.Extensions</c>.
/// <para>
/// Um schema transformer não alcança essas propriedades: elas não têm
/// <c>JsonPropertyInfo</c> nomeado — chegam pelo dicionário de extensões, que o
/// gerador descreve como <c>additionalProperties</c> e não como membro. Daí a
/// declaração ser de documento, sobre o schema já emitido.
/// </para>
/// <para>
/// Cobre <c>retryable</c>, que a ADR-0134 usa para distinguir o conflito que
/// vale repetir daquele que só muda de estado por outra ação. Sem a declaração o
/// campo sai na resposta e não existe no contrato: quem gera cliente tipado a
/// partir do spec não tem propriedade para consultar, que é o oposto do que a
/// decisão promete.
/// </para>
/// <para>
/// Cobre também <c>errors</c>, a lista das violações da ADR-0023, com o campo, o código, a
/// mensagem e os identificadores dos itens de lista que o campo atravessa.
/// </para>
/// </summary>
public sealed class ProblemDetailsExtensionsDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string ProblemDetailsSchemaName = "ProblemDetails";
    private const string RetryablePropertyName = ResultExtensions.RetryableExtensionName;

    private const string RetryableDescription =
        "Presente e `true` quando o conflito descreve uma corrida que já passou: a mesma "
        + "requisição, repetida sem alteração, pode agora ser aceita. A ausência é o default "
        + "conservador do servidor — diz que o conflito não foi declarado retentável, não que "
        + "repetir esteja descartado; nesse caso vale a descrição da própria resposta.";

    private const string ErrorsPropertyName = "errors";

    private const string ErrorsDescription =
        "Uma entrada por violação, quando a resposta reúne mais de uma ou quando alguma aponta um campo. "
        + "A primeira é a que descreve a resposta inteira.";

    private const string FieldDescription =
        "O campo que a violação aponta, em notação de pontos e colchetes ou como ponteiro JSON quando o corpo é um documento. "
        + "Ausente quando a violação não é de campo.";

    private const string IdentificadoresDescription =
        "O identificador estável de cada item de lista que o campo atravessa, na ordem do caminho, para achar o item "
        + "mesmo depois de outra gravação mudar as posições. Ausente quando a violação não aponta item com identificador; "
        + "o identificador que já não existe não aponta outro item.";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        if (document.Components?.Schemas?.TryGetValue(ProblemDetailsSchemaName, out IOpenApiSchema? schema) != true
            || schema is not OpenApiSchema problemDetails)
        {
            return Task.CompletedTask;
        }

        DeclararAsExtensoes(problemDetails);

        return Task.CompletedTask;
    }

    private static void DeclararAsExtensoes(OpenApiSchema problemDetails)
    {
        problemDetails.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        // A propriedade só viaja quando é `true` (ResultExtensions grava a extensão
        // apenas nesse caso), então fica fora de `required`: cliente que a espera
        // sempre presente leria a ausência como erro de contrato.
        problemDetails.Properties[RetryablePropertyName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Boolean,
            Description = RetryableDescription,
        };

        problemDetails.Properties[ErrorsPropertyName] = ErrosDeCampo();
    }

    private static OpenApiSchema ErrosDeCampo() => new()
    {
        Type = JsonSchemaType.Array,
        Description = ErrorsDescription,
        Items = ItemDeErro(),
    };

    private static OpenApiSchema ItemDeErro() => new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string>(["code", "message"], StringComparer.Ordinal),
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["field"] = Texto(FieldDescription),
            ["code"] = Texto("O código publicado do erro."),
            ["message"] = Texto("A mensagem do erro, que nunca repete o valor recusado."),
            ["identificadores"] = Identificadores(),
        },
    };

    private static OpenApiSchema Texto(string descricao) => new() { Type = JsonSchemaType.String, Description = descricao };

    private static OpenApiSchema Identificadores() => new()
    {
        Type = JsonSchemaType.Array,
        Description = IdentificadoresDescription,
        Items = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string>(["lista", "identificador"], StringComparer.Ordinal),
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["lista"] = Texto("O nome da lista, como aparece no caminho do campo."),
                ["identificador"] = Texto("O identificador estável do item: um código de catálogo ou um identificador opaco, nunca dado pessoal."),
            },
        },
    };
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Publica o parâmetro de consulta marcado com <see cref="AceitaCodigosDeAttribute{TEnum}"/> como referência ao componente
/// dos códigos do enum, em vez de texto livre. O parâmetro continua opcional ou obrigatório como o gerador o publicou.
/// </summary>
internal sealed class AceitaCodigosDeOperationTransformer : IOpenApiOperationTransformer
{
    private readonly ComponentesDosCodigos _codigos;

    public AceitaCodigosDeOperationTransformer(ComponentesDosCodigos codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        _codigos = codigos;
    }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        string nomeDaOperacao = $"{context.Description.HttpMethod} /{context.Description.RelativePath}";

        foreach (ApiParameterDescription descricao in context.Description.ParameterDescriptions)
        {
            if (CampoQueAceitaCodigos.De(descricao, nomeDaOperacao) is { } campo)
            {
                Publicar(operation, descricao.Name, campo, context.Document);
            }
        }

        return Task.CompletedTask;
    }

    private void Publicar(OpenApiOperation operation, string nome, CampoQueAceitaCodigos campo, OpenApiDocument? documento)
    {
        if (operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(p => string.Equals(p.Name, nome, StringComparison.Ordinal))
            is { Schema: { } schema } parametro)
        {
            parametro.Schema = _codigos.ParaCampo(schema, campo, documento);
        }
    }
}

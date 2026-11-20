namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Publica o formato com que a resposta expõe um enum de contrato, <see cref="CodigoComRotulo{TEnum}"/> ou
/// <see cref="CodigoComDescricao{TEnum}"/>: o <c>codigo</c> passa a referenciar o componente dos códigos do enum, em vez de
/// carregar a lista. A explicação do enum e a de cada código ficam no componente dos códigos, e o componente do formato não as
/// repete. Sem isto o gerador, que não conhece o catálogo, deixaria o campo como texto livre. A referência entra no documento
/// pronto, por <see cref="ComponentesDosCodigos"/>: o gerador troca por componente inteiro a referência que um schema leva.
/// </summary>
internal sealed class FormatosDeCodigoSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string NomeDaPropriedade = "codigo";

    private readonly ComponentesDosCodigos _codigos;

    public FormatosDeCodigoSchemaTransformer(ComponentesDosCodigos codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        _codigos = codigos;
    }

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (FormatoDeCodigo.De(context.JsonTypeInfo.Type) is { } formato)
        {
            Publicar(formato);
        }

        return Task.CompletedTask;
    }

    private void Publicar(FormatoDeCodigo formato)
    {
        if (formato.ComDescricao)
        {
            ExigirDescricaoEmTodosOsCodigos(formato.EnumDeContrato);
        }

        _codigos.Registrar(formato.NomeDoComponente, NomeDaPropriedade, CampoQueAceitaCodigos.DoCodigoDe(formato.EnumDeContrato));
    }

    private static void ExigirDescricaoEmTodosOsCodigos(Type enumDeContrato)
    {
        string[] semDescricao =
        [
            .. EnumsDeContrato.Declarados(enumDeContrato).Where(static d => d.Descricao is null).Select(static d => $"'{d.Codigo}'"),
        ];

        if (semDescricao.Length > 0)
        {
            throw new InvalidOperationException(
                $"O formato com descrição exige descrição em todo membro do enum {enumDeContrato.FullName}, e faltam nos códigos "
                + $"{string.Join(", ", semDescricao)}. Declare a descrição ou use o formato com rótulo.");
        }
    }
}

namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Os componentes <c>{Enum}Codigo</c> de um documento OpenAPI: um por enum de contrato que o documento referencia, com os
/// códigos que o enum declarou e a explicação do enum e a de cada código. Os transformers de schema registram o campo que leva os códigos
/// de um enum, e este transformer de documento, que roda quando o documento está pronto, troca cada um por uma referência
/// ao componente dos códigos: o gerador troca por componente inteiro a referência que um schema leva, e só o
/// <c>$ref</c> escrito depois dele chega ao contrato. Um objeto por documento, e o mesmo nome nunca serve a dois tipos.
/// </summary>
internal sealed class ComponentesDosCodigos : IOpenApiDocumentTransformer
{
    private readonly NomesDosComponentes _nomes;
    private readonly ConcurrentDictionary<(string Componente, string Propriedade), CampoQueAceitaCodigos> _campos = new();
    private readonly ConditionalWeakTable<OpenApiDocument, ConcurrentDictionary<Type, OpenApiSchema>> _criados = [];

    public ComponentesDosCodigos(NomesDosComponentes nomes)
    {
        ArgumentNullException.ThrowIfNull(nomes);

        _nomes = nomes;
    }

    /// <summary>Registra a propriedade de um componente que leva os códigos de um enum.</summary>
    public void Registrar(string componente, string propriedade, CampoQueAceitaCodigos campo)
    {
        ArgumentNullException.ThrowIfNull(campo);

        _campos.TryAdd((componente, propriedade), campo);
    }

    /// <summary>Registra a propriedade do tipo, que o gerador publica como componente, que leva os códigos de um enum.</summary>
    public void Registrar(JsonTypeInfo tipo, string propriedade, CampoQueAceitaCodigos campo)
    {
        if (_nomes.Nome(tipo) is { } componente)
        {
            Registrar(componente, propriedade, campo);
        }
    }

    /// <summary>Troca cada propriedade registrada pela referência ao componente dos códigos do seu enum.</summary>
    /// <exception cref="InvalidOperationException">Outro tipo ou componente do documento já tem o nome do componente.</exception>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (((string componente, string propriedade), CampoQueAceitaCodigos campo) in _campos.OrderBy(static c => c.Key))
        {
            if (PropriedadesDe(document, componente) is { } propriedades && propriedades.TryGetValue(propriedade, out IOpenApiSchema? atual))
            {
                propriedades[propriedade] = ParaCampo(atual, campo, document);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// O texto trocado pela referência ao componente dos códigos do enum, anulável quando o texto é. O texto não perde a
    /// descrição que já tinha.
    /// </summary>
    /// <exception cref="InvalidOperationException">Outro tipo ou componente do documento já tem o nome do componente.</exception>
    public IOpenApiSchema ParaTexto(IOpenApiSchema texto, Type enumDeContrato, OpenApiDocument? documento)
    {
        ArgumentNullException.ThrowIfNull(texto);

        OpenApiSchemaReference referencia = Referenciar(enumDeContrato, documento);

        return texto.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Null) ? Anulavel(texto, referencia) : ComDescricaoDe(texto, referencia);
    }

    /// <summary>
    /// O campo com a referência no próprio campo, quando é um texto, ou em cada item, quando é uma coleção de textos. A
    /// obrigatoriedade do campo e a nulabilidade do campo e da coleção ficam como o gerador as publicou. O item só aceita
    /// código válido: o nulo que o domínio recebe para recusar não entra como item aceito.
    /// </summary>
    /// <exception cref="InvalidOperationException">Outro tipo ou componente do documento já tem o nome do componente.</exception>
    public IOpenApiSchema ParaCampo(IOpenApiSchema campo, CampoQueAceitaCodigos alvo, OpenApiDocument? documento)
    {
        ArgumentNullException.ThrowIfNull(campo);
        ArgumentNullException.ThrowIfNull(alvo);

        if (!alvo.EhColecao)
        {
            return ParaTexto(campo, alvo.EnumDeContrato, documento);
        }

        if (campo is OpenApiSchema { Items: not null } colecao)
        {
            colecao.Items = Referenciar(alvo.EnumDeContrato, documento);
        }

        return campo;
    }

    private static IDictionary<string, IOpenApiSchema>? PropriedadesDe(OpenApiDocument documento, string componente) =>
        documento.Components?.Schemas is { } schemas && schemas.TryGetValue(componente, out IOpenApiSchema? dono)
            ? (dono as OpenApiSchema)?.Properties
            : null;

    private OpenApiSchemaReference Referenciar(Type enumDeContrato, OpenApiDocument? documento) =>
        new(Publicar(enumDeContrato, documento), documento);

    private string Publicar(Type enumDeContrato, OpenApiDocument? documento)
    {
        string nome = _nomes.NomeDosCodigos(enumDeContrato);

        if (documento is not null)
        {
            OpenApiSchema componente = _criados.GetOrCreateValue(documento).GetOrAdd(enumDeContrato, ComponenteDe);

            ExigirQueOComponenteSejaOCriado(documento, nome, componente, enumDeContrato);
        }

        return nome;
    }

    private static void ExigirQueOComponenteSejaOCriado(OpenApiDocument documento, string nome, OpenApiSchema componente, Type enumDeContrato)
    {
        if (!documento.AddComponent(nome, componente) && !ReferenceEquals(documento.Components?.Schemas?[nome], componente))
        {
            throw new InvalidOperationException(
                $"O componente '{nome}' do documento OpenAPI serviria aos códigos de {enumDeContrato.FullName} e a um componente que o "
                + "documento já tem. Dê nomes diferentes aos tipos ou aos enums.");
        }
    }

    private static OpenApiSchema Anulavel(IOpenApiSchema texto, OpenApiSchemaReference referencia) => new()
    {
        Description = texto.Description,
        OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, referencia],
    };

    private static OpenApiSchemaReference ComDescricaoDe(IOpenApiSchema texto, OpenApiSchemaReference referencia)
    {
        referencia.Description = texto.Description;

        return referencia;
    }

    private static OpenApiSchema ComponenteDe(Type enumDeContrato)
    {
        IReadOnlyList<CodigoDeclarado> declarados = EnumsDeContrato.Declarados(enumDeContrato);

        return new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [.. declarados.Select(static d => (JsonNode)JsonValue.Create(d.Codigo))],
            Description = ExplicacaoDosCodigos(EnumsDeContrato.Descricao(enumDeContrato), declarados),
        };
    }

    // A description do OpenAPI é CommonMark: um item de lista por código, e uma linha em branco entre o parágrafo do enum e a lista.
    private static string ExplicacaoDosCodigos(string? descricaoDoEnum, IReadOnlyList<CodigoDeclarado> declarados)
    {
        string lista = string.Join('\n', declarados.Select(Linha));

        return descricaoDoEnum is null ? lista : $"{descricaoDoEnum}\n\n{lista}";
    }

    private static string Linha(CodigoDeclarado declarado) =>
        declarado.Descricao is null
            ? $"- `{declarado.Codigo}`: {declarado.Rotulo}"
            : $"- `{declarado.Codigo}`: {declarado.Rotulo} — {declarado.Descricao}";
}

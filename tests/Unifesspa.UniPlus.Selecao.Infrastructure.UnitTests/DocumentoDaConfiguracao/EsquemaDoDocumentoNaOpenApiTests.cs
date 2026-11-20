namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>O esquema do documento entra no documento OpenAPI como componentes escritos como o esquema os declara.</summary>
public sealed class EsquemaDoDocumentoNaOpenApiTests
{
    private static SortedDictionary<string, JsonNode> ComponentesDoEsquemaReal() =>
        new ComponentesDoEsquema(ArquivosDoEsquema.DoAssembly().PorNome).Gerar();

    private static async Task<OpenApiDocument> Transformar(OpenApiDocument? documento = null)
    {
        OpenApiDocument alvo = documento ?? new OpenApiDocument();
        await new EsquemaDoDocumentoNaOpenApi().TransformAsync(alvo, new OpenApiDocumentTransformerContext
        {
            DocumentName = "selecao",
            DescriptionGroups = [],
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        }, CancellationToken.None);
        return alvo;
    }

    [Fact(DisplayName = "Cada componente do esquema entra no documento, em ordem ordinal")]
    public async Task Transformar_PublicaOsComponentesEmOrdemOrdinal()
    {
        OpenApiDocument documento = await Transformar();

        string[] nomes = [.. documento.Components!.Schemas!.Keys];
        nomes.Should().Equal(ComponentesDoEsquemaReal().Keys);
        nomes.Should().BeInAscendingOrder(StringComparer.Ordinal);
        nomes.Should().Contain(EsquemaPublicadoDoDocumento.Componente);
    }

    [Fact(DisplayName = "Cada componente publicado é escrito como o esquema o declara")]
    public async Task Transformar_EscreveCadaComponenteComoGerado()
    {
        OpenApiDocument documento = await Transformar();

        foreach ((string nome, JsonNode esperado) in ComponentesDoEsquemaReal())
        {
            using StringWriter texto = new();
            documento.Components!.Schemas![nome].SerializeAsV31(new OpenApiJsonWriter(texto));

            JsonNode.DeepEquals(JsonNode.Parse(texto.ToString()), esperado).Should().BeTrue(nome);
        }
    }

    [Fact(DisplayName = "Cada geração cria schemas novos e preserva os componentes que o documento já tinha")]
    public async Task Transformar_NaoCompartilhaInstanciaEntreDocumentos()
    {
        OpenApiSchema existente = new();
        OpenApiDocument primeiro = await Transformar(new OpenApiDocument { Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema> { ["Existente"] = existente } } });
        OpenApiDocument segundo = await Transformar();

        primeiro.Components!.Schemas!["Existente"].Should().BeSameAs(existente);
        primeiro.Components.Schemas[EsquemaPublicadoDoDocumento.Componente].Should()
            .NotBeSameAs(segundo.Components!.Schemas![EsquemaPublicadoDoDocumento.Componente]);
    }

    [Fact(DisplayName = "Um componente que o documento já tem com o nome de um do esquema faz a geração falhar, em vez de ser trocado")]
    public async Task Transformar_ComponenteExistenteComOMesmoNome_Falha()
    {
        OpenApiSchema existente = new();
        OpenApiDocument documento = new() { Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema> { [EsquemaPublicadoDoDocumento.Componente] = existente } } };

        Func<Task> geracao = () => Transformar(documento);

        (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain($"'{EsquemaPublicadoDoDocumento.Componente}'");
        IOpenApiSchema? mantido = documento.Components?.Schemas?[EsquemaPublicadoDoDocumento.Componente];
        mantido.Should().BeSameAs(existente);
    }

    [Fact(DisplayName = "O documento é obrigatório")]
    public async Task DocumentoNulo_Lanca()
    {
        Func<Task> semDocumento = () => new EsquemaDoDocumentoNaOpenApi().TransformAsync(null!, null!, CancellationToken.None);

        await semDocumento.Should().ThrowAsync<ArgumentNullException>();
    }
}

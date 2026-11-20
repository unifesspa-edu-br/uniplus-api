namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CodigoComRotuloSchemaTransformerTests
{
    private const string Documento = "codigo-com-rotulo";

    private static readonly Lazy<Task<OpenApiDocument>> DocumentoGerado = new(GerarDocumentoAsync);

    [DescricaoDoContrato("Como anda o trabalho, para todos os códigos.")]
    public enum Andamento
    {
        [CodigoDeContrato("em_andamento", "Em andamento", Descricao = "Ainda há o que fazer.")]
        EmAndamento = 1,

        [CodigoDeContrato("concluido", "Concluído")]
        Concluido = 2,
    }

    [Fact(DisplayName = "O código com rótulo vira um componente com o nome do enum, sem o tipo genérico")]
    public async Task Componente_TemONomeDoEnum()
    {
        IDictionary<string, IOpenApiSchema> componentes = await ComponentesAsync();

        componentes.Keys.Should().Contain(nameof(Andamento)).And.NotContain(chave => chave.Contains("CodigoComRotulo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O código é publicado como texto com a lista dos códigos que o enum declarou")]
    public async Task Codigo_PublicadoComAListaDosCodigos()
    {
        IOpenApiSchema codigo = await PropriedadeAsync("codigo");

        codigo.Type.Should().Be(JsonSchemaType.String);
        (codigo.Enum ?? []).Select(static n => n?.GetValue<string>()).Should().Equal("em_andamento", "concluido");
    }

    [Fact(DisplayName = "A descrição do código lista cada código com o rótulo e, quando há, a descrição")]
    public async Task Codigo_DescricaoListaCadaCodigo()
    {
        IOpenApiSchema codigo = await PropriedadeAsync("codigo");

        codigo.Description.Should().Be("`em_andamento`: Em andamento — Ainda há o que fazer.\n`concluido`: Concluído");
    }

    [Fact(DisplayName = "A descrição do enum vai para o componente, e não para a propriedade do código")]
    public async Task DescricaoDoEnum_VaiParaOComponente()
    {
        IOpenApiSchema componente = await ComponenteAsync(nameof(Andamento));

        componente.Description.Should().Be("Como anda o trabalho, para todos os códigos.");
        PropriedadeDe(componente, "codigo").Description.Should().NotContain("para todos os códigos");
    }

    [Fact(DisplayName = "O enum sem a descrição do enum publica o componente sem descrição")]
    public async Task EnumSemDescricao_ComponenteSemDescricao()
    {
        IOpenApiSchema componente = await ComponenteAsync(nameof(Etapa));

        componente.Description.Should().BeNull();
        PropriedadeDe(componente, "codigo").Enum.Should().ContainSingle();
    }

    [Fact(DisplayName = "O rótulo é publicado como texto livre")]
    public async Task Rotulo_PublicadoComoTexto()
    {
        IOpenApiSchema rotulo = await PropriedadeAsync("rotulo");

        rotulo.Type.Should().Be(JsonSchemaType.String);
        rotulo.Enum.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName = "O código e o rótulo são obrigatórios")]
    public async Task CodigoERotulo_SaoObrigatorios()
    {
        IOpenApiSchema componente = await ComponenteAsync(nameof(Andamento));

        componente.Required.Should().BeEquivalentTo(["codigo", "rotulo"]);
    }

    [Fact(DisplayName = "O texto de outra propriedade chamada codigo não recebe a lista de códigos")]
    public async Task OutraPropriedadeCodigo_NaoRecebeALista()
    {
        IOpenApiSchema codigo = PropriedadeDe(await ComponenteAsync("Outro"), "codigo");

        codigo.Enum.Should().BeNullOrEmpty();
    }

    private static async Task<IOpenApiSchema> PropriedadeAsync(string nome) =>
        PropriedadeDe(await ComponenteAsync(nameof(Andamento)), nome);

    private static async Task<IDictionary<string, IOpenApiSchema>> ComponentesAsync()
    {
        OpenApiDocument documento = await DocumentoGerado.Value;

        return documento.Components?.Schemas ?? throw new InvalidOperationException("O documento gerado não tem componentes.");
    }

    private static async Task<IOpenApiSchema> ComponenteAsync(string nome)
    {
        IDictionary<string, IOpenApiSchema> componentes = await ComponentesAsync();

        return componentes.TryGetValue(nome, out IOpenApiSchema? componente)
            ? componente
            : throw new InvalidOperationException($"O documento gerado não tem o componente {nome}.");
    }

    private static IOpenApiSchema PropriedadeDe(IOpenApiSchema componente, string nome) =>
        componente.Properties is { } propriedades && propriedades.TryGetValue(nome, out IOpenApiSchema? propriedade)
            ? propriedade
            : throw new InvalidOperationException($"O componente não tem a propriedade {nome}.");

    private static async Task<OpenApiDocument> GerarDocumentoAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddUniPlusJsonOptions();
        builder.Services.AddUniPlusOpenApi(Documento, builder.Configuration);
        await using WebApplication app = builder.Build();
        app.MapGet("/andamento", () => new Resposta(CodigoComRotulo<Andamento>.De(Andamento.EmAndamento)));
        app.MapGet("/etapa", () => new RespostaDaEtapa(CodigoComRotulo<Etapa>.De(Etapa.Unica)));
        app.MapGet("/outro", () => new Outro("abc"));
        await app.StartAsync();

        return await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(Documento).GetOpenApiDocumentAsync();
    }

    public enum Etapa
    {
        [CodigoDeContrato("unica", "Única")]
        Unica = 1,
    }

    public sealed record Resposta(CodigoComRotulo<Andamento> Andamento);

    public sealed record RespostaDaEtapa(CodigoComRotulo<Etapa> Etapa);

    public sealed record Outro(string Codigo);
}

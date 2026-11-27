namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class UniPlusSchemaTransformerTests
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    private static readonly string[] Todas = ["sempre", "omitidaSeNula", "omitidaSeDefault", "protegida", "item", "token"];

    [Fact(DisplayName = "Fica fora de required o que a serialização pode omitir; o que sempre sai segue obrigatório")]
    public async Task Required_RetiraSoOQueAOmissaoAlcanca()
    {
        OpenApiSchema schema = new() { Required = new HashSet<string>(Todas.Except(["token"]), StringComparer.Ordinal) };

        await new UniPlusSchemaTransformer().TransformAsync(schema, ContextoDoTipo(), CancellationToken.None);

        schema.Required.Should().BeEquivalentTo(["sempre", "token"]);
    }

    [Fact(DisplayName = "O objeto aninhado por propriedade única é ajustado: o pipeline o alcança com o JsonPropertyInfo do pai")]
    public async Task Required_AjustaOObjetoAninhadoPorPropriedadeUnica()
    {
        OpenApiSchema schema = new() { Required = new HashSet<string>(["sempre", "omitidaSeNula"], StringComparer.Ordinal) };

        await new UniPlusSchemaTransformer().TransformAsync(schema, ContextoDaPropriedade<Pai>("aninhado"), CancellationToken.None);

        schema.Required.Should().BeEquivalentTo(["sempre"]);
    }

    [Fact(DisplayName = "Sem nada obrigatório sobrando o schema fica sem required")]
    public async Task Required_VazioFicaNulo()
    {
        OpenApiSchema schema = new() { Required = new HashSet<string>(["omitidaSeNula"], StringComparer.Ordinal) };

        await new UniPlusSchemaTransformer().TransformAsync(
            schema,
            new OpenApiSchemaTransformerContext { DocumentName = "t", JsonTypeInfo = TipoDe<SoOmissivel>(), JsonPropertyInfo = null, ParameterDescription = null, ApplicationServices = null! },
            CancellationToken.None);

        schema.Required.Should().BeNull();
    }

    [Fact(DisplayName = "O token de classificação sai como texto não anulável com os cinco valores")]
    public async Task Token_SaiComoEnumFechado()
    {
        OpenApiSchema schema = new() { Type = JsonSchemaType.String | JsonSchemaType.Null };

        await new UniPlusSchemaTransformer().TransformAsync(schema, ContextoDaPropriedade<Dto>("token"), CancellationToken.None);

        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Enum!.Select(static n => n!.GetValue<string>()).Should()
            .BeEquivalentTo(["PUBLICO", "INTERNO", "PESSOAL", "IDENTIFICADOR", "SENSIVEL"]);
    }

    [Fact(DisplayName = "O campo obrigatório no contrato entra em required do objeto e sai sem aceitar nulo")]
    public async Task ObrigatorioNoContrato_EntraEmRequiredESaiSemNulo()
    {
        OpenApiSchema objeto = new();
        OpenApiSchema campo = new() { Type = JsonSchemaType.String | JsonSchemaType.Null };
        UniPlusSchemaTransformer transformer = new();

        await transformer.TransformAsync(objeto, ContextoDoTipo<ComCampoObrigatorio>(), CancellationToken.None);
        await transformer.TransformAsync(campo, ContextoDaPropriedade<ComCampoObrigatorio>("codigo"), CancellationToken.None);

        objeto.Required.Should().BeEquivalentTo(["codigo"]);
        campo.Type.Should().Be(JsonSchemaType.String);
    }

    [Theory(DisplayName = "O texto marcado como código, anulável ou não, publica o padrão do código e o limite")]
    [InlineData("codigo", JsonSchemaType.String)]
    [InlineData("codigoAnulavel", JsonSchemaType.String | JsonSchemaType.Null)]
    public async Task NoFormatoDoCodigo_PublicaPadraoELimite(string campo, JsonSchemaType tipo)
    {
        OpenApiSchema schema = new() { Type = tipo };

        await new UniPlusSchemaTransformer().TransformAsync(schema, ContextoDaPropriedade<ComCodigoMarcado>(campo), CancellationToken.None);

        schema.Pattern.Should().Be("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$");
        schema.MaxLength.Should().Be(60);
        schema.Type.Should().Be(tipo);
    }

    [Fact(DisplayName = "O campo sem a marca de código não ganha padrão nem limite")]
    public async Task SemMarca_NaoPublicaPadrao()
    {
        OpenApiSchema schema = new() { Type = JsonSchemaType.String };

        await new UniPlusSchemaTransformer().TransformAsync(schema, ContextoDaPropriedade<ComCodigoMarcado>("livre"), CancellationToken.None);

        schema.Pattern.Should().BeNull();
        schema.MaxLength.Should().BeNull();
    }

    [Theory(DisplayName = "A marca de código recusa o campo que não é texto ou que já aceita códigos de um enum")]
    [InlineData(typeof(ComCodigoMarcadoEmNumero), "numero")]
    [InlineData(typeof(ComCodigoMarcadoQueAceitaFase), "fase")]
    public async Task NoFormatoDoCodigo_CampoInvalido_Lanca(Type pai, string campo)
    {
        OpenApiSchema schema = new() { Type = JsonSchemaType.String };
        OpenApiSchemaTransformerContext contexto = ContextoDaPropriedade(pai, campo);

        Func<Task> aplicar = () => new UniPlusSchemaTransformer().TransformAsync(schema, contexto, CancellationToken.None);

        (await aplicar.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("NoFormatoDoCodigo");
    }

    private static OpenApiSchemaTransformerContext ContextoDoTipo() => ContextoDoTipo<Dto>();

    private static OpenApiSchemaTransformerContext ContextoDoTipo<T>() => new()
    {
        DocumentName = "t",
        JsonTypeInfo = TipoDe<T>(),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = null!,
    };

    /// <summary>
    /// O contexto da forma que o pipeline passa para o schema de uma propriedade: o <c>JsonPropertyInfo</c> é o da propriedade
    /// do pai e o <c>JsonTypeInfo</c> é o do tipo dela, que para o objeto aninhado é o do próprio objeto.
    /// </summary>
    private static OpenApiSchemaTransformerContext ContextoDaPropriedade<TPai>(string nome) =>
        ContextoDaPropriedade(typeof(TPai), nome);

    private static OpenApiSchemaTransformerContext ContextoDaPropriedade(Type pai, string nome)
    {
        JsonPropertyInfo propriedade = new DefaultJsonTypeInfoResolver().GetTypeInfo(pai, Opcoes)!.Properties.Single(p => p.Name == nome);
        return new()
        {
            DocumentName = "t",
            JsonTypeInfo = new DefaultJsonTypeInfoResolver().GetTypeInfo(propriedade.PropertyType, Opcoes)!,
            JsonPropertyInfo = propriedade,
            ParameterDescription = null,
            ApplicationServices = null!,
        };
    }

    private static JsonTypeInfo TipoDe<T>() => new DefaultJsonTypeInfoResolver().GetTypeInfo(typeof(T), Opcoes)!;

    public sealed class Pai
    {
        public Aninhado Aninhado { get; init; } = new();
    }

    public sealed class Aninhado
    {
        public string Sempre { get; init; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OmitidaSeNula { get; init; }
    }

    public sealed class SoOmissivel
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OmitidaSeNula { get; init; }
    }

    public sealed class Dto
    {
        public string Sempre { get; init; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OmitidaSeNula { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int OmitidaSeDefault { get; init; }

        [ProtegidoComo(ClassificacaoDeProtecao.Pessoal)]
        public string Protegida { get; init; } = "";

        public Item? Item { get; init; }

        [TokenDeClassificacao]
        public string Token { get; init; } = "";
    }

    public sealed class ComCampoObrigatorio
    {
        [ObrigatorioNoContrato]
        public string? Codigo { get; init; }
    }

    public sealed class ComCodigoMarcado
    {
        [NoFormatoDoCodigo(60)]
        public string Codigo { get; init; } = "";

        [NoFormatoDoCodigo(60)]
        public string? CodigoAnulavel { get; init; }

        public string Livre { get; init; } = "";
    }

    public sealed class ComCodigoMarcadoEmNumero
    {
        [NoFormatoDoCodigo(60)]
        public int Numero { get; init; }
    }

    public sealed class ComCodigoMarcadoQueAceitaFase
    {
        [NoFormatoDoCodigo(60)]
        [AceitaCodigosDe<ModeloDeCodigosDeTeste.Fase>]
        public string Fase { get; init; } = "";
    }

    public sealed record Item() : ItemClassificado(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, null);
}

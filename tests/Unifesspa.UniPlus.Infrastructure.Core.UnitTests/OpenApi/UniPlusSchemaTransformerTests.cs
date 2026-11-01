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
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

    private static OpenApiSchemaTransformerContext ContextoDoTipo() => new()
    {
        DocumentName = "t",
        JsonTypeInfo = TipoDe<Dto>(),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = null!,
    };

    /// <summary>
    /// O contexto da forma que o pipeline passa para o schema de uma propriedade: o <c>JsonPropertyInfo</c> é o da propriedade
    /// do pai e o <c>JsonTypeInfo</c> é o do tipo dela, que para o objeto aninhado é o do próprio objeto.
    /// </summary>
    private static OpenApiSchemaTransformerContext ContextoDaPropriedade<TPai>(string nome)
    {
        JsonPropertyInfo propriedade = TipoDe<TPai>().Properties.Single(p => p.Name == nome);
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

    public sealed record Item() : ItemClassificado(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, null);
}

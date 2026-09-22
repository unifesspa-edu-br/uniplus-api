namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Dois erros de forma equivalentes do cliente, no mesmo campo, produziam status incoerentes:
/// discriminador desconhecido devolvia 400 e discriminador <b>ausente</b> devolvia 500, porque o
/// desserializador reporta o segundo como <see cref="NotSupportedException"/> e o formatter do
/// framework só converte <see cref="JsonException"/> (issue #1410).
/// </summary>
public sealed class PolymorphicDeserializationInputFormatterTests
{
    /// <summary>
    /// A premissa de que o resto depende: sem o envoltório a recusa escapa da leitura do corpo e
    /// chega ao tratador global, que não tem como saber que o defeito é do cliente.
    /// </summary>
    [Fact(DisplayName = "Sem o envoltório, discriminador ausente escapa da leitura como falha inesperada")]
    public async Task ReadAsync_SemOEnvoltorio_DeixaAExcecaoEscapar()
    {
        InputFormatterContext contexto = Contexto<Comando>("""{"predicado":{}}""");

        Func<Task> ler = () => FormatterDoFramework().ReadAsync(contexto);

        await ler.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact(DisplayName = "Discriminador ausente vira recusa de leitura, e não exceção")]
    public async Task ReadAsync_DiscriminadorAusente_RecusaSemLancar()
    {
        InputFormatterContext contexto = Contexto<Comando>("""{"predicado":{}}""");

        InputFormatterResult resultado = await Formatter().ReadAsync(contexto);

        resultado.HasError.Should().BeTrue();
        contexto.ModelState.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A chave é o que faz a recusa ser reconhecida como de LEITURA do corpo por
    /// <see cref="InvalidRequestProblemFactory"/> — sem ela a resposta sairia como campo
    /// obrigatório ausente, mandando declarar o que já foi declarado.
    /// </summary>
    [Fact(DisplayName = "A recusa entra sob o caminho do erro dentro do documento")]
    public async Task ReadAsync_DiscriminadorAusente_EntraSobOCaminhoDoDocumento()
    {
        InputFormatterContext contexto = Contexto<Comando>("""{"predicado":{}}""");

        await Formatter().ReadAsync(contexto);

        contexto.ModelState.Should().ContainKey("$.predicado");
    }

    /// <summary>
    /// É dela que sairia o nome completo do tipo CLR que falhou a desserialização — namespace,
    /// camada e nome do record da Application entregues a quem mandou uma carga incompleta.
    /// </summary>
    [Fact(DisplayName = "A mensagem do framework não entra no ModelState")]
    public async Task ReadAsync_DiscriminadorAusente_NaoGuardaAMensagemDoFramework()
    {
        InputFormatterContext contexto = Contexto<Comando>("""{"predicado":{}}""");

        await Formatter().ReadAsync(contexto);

        contexto.ModelState["$.predicado"]!.Errors
            .Should().OnlyContain(erro => erro.ErrorMessage.Length == 0 && erro.Exception == null);
    }

    [Theory(DisplayName = "O campo e o discriminador esperado são anotados para a resposta")]
    [InlineData("""{"predicado":{}}""", "predicado")]
    [InlineData("""{"predicado":{"tipo":"etapa"}}""", "predicado")]
    [InlineData("""{"itens":[{"predicado":{}}]}""", "itens[0].predicado")]
    public async Task ReadAsync_DiscriminadorAusente_AnotaCampoEDiscriminador(string corpo, string campo)
    {
        InputFormatterContext contexto = Contexto<Comando>(corpo);

        await Formatter().ReadAsync(contexto);

        PolymorphicDeserializationFailures.Of(contexto.HttpContext)
            .Should().Be(new PolymorphicDeserializationFailures.Falha(campo, "$tipo"));
    }

    /// <summary>
    /// Quando a união é o corpo inteiro não há campo a nomear, e a resposta precisa saber disso
    /// para falar do corpo em vez de inventar um nome.
    /// </summary>
    [Fact(DisplayName = "União na raiz é anotada sem campo")]
    public async Task ReadAsync_UniaoNaRaiz_AnotaSemCampo()
    {
        InputFormatterContext contexto = Contexto<Predicado>("{}");

        await Formatter().ReadAsync(contexto);

        PolymorphicDeserializationFailures.Of(contexto.HttpContext)
            .Should().Be(new PolymorphicDeserializationFailures.Falha(string.Empty, "$tipo"));
    }

    /// <summary>
    /// O nome vem do contrato do modelo, e não de constante nossa: união que discrimine por
    /// outra propriedade é anunciada pela propriedade dela.
    /// </summary>
    [Fact(DisplayName = "O discriminador anunciado é o que o contrato declara, qualquer que seja")]
    public async Task ReadAsync_OutroDiscriminador_AnunciaODoContrato()
    {
        InputFormatterContext contexto = Contexto<ComandoDeOutraUniao>("""{"forma":{}}""");

        await Formatter().ReadAsync(contexto);

        PolymorphicDeserializationFailures.Of(contexto.HttpContext)
            .Should().Be(new PolymorphicDeserializationFailures.Falha("forma", "discriminadorProprio"));
    }

    /// <summary>
    /// Discriminador desconhecido e corpo não-objeto já eram recusados pelo formatter do
    /// framework, que os reporta como <see cref="JsonException"/>. Continuam por aquele caminho,
    /// e sem anotação: ali o cliente declarou a variante, ela é que não existe.
    /// </summary>
    [Theory(DisplayName = "As recusas que o framework já tratava seguem intactas e sem anotação")]
    [InlineData("""{"predicado":{"$tipo":"inexistente"}}""")]
    [InlineData("\"lixo\"")]
    public async Task ReadAsync_RecusasJaTratadas_NaoAnotam(string corpo)
    {
        InputFormatterContext contexto = Contexto<Comando>(corpo);

        InputFormatterResult resultado = await Formatter().ReadAsync(contexto);

        resultado.HasError.Should().BeTrue();
        PolymorphicDeserializationFailures.Of(contexto.HttpContext).Should().BeNull();
    }

    /// <summary>
    /// A mesma <see cref="NotSupportedException"/> sai de um tipo abstrato que ninguém declarou
    /// como união — e aí o defeito é do modelo do servidor, não do documento.
    /// </summary>
    /// <remarks>
    /// Rebaixar isso a 400 tiraria do alerta o único defeito deste caminho que o cliente não tem
    /// como corrigir: toda requisição àquele endpoint passaria a recusar um JSON perfeito,
    /// dizendo que o corpo é que está errado. O que separa os dois casos é o contrato, não a
    /// frase do framework: ali não há união polimórfica nenhuma declarada.
    /// </remarks>
    [Fact(DisplayName = "Tipo abstrato que não é união segue como falha de servidor")]
    public async Task ReadAsync_TipoAbstratoSemUniao_DeixaAExcecaoEscapar()
    {
        InputFormatterContext contexto = Contexto<ComandoMalConfigurado>("""{"valor":{}}""");

        Func<Task> ler = () => Formatter().ReadAsync(contexto);

        await ler.Should().ThrowAsync<NotSupportedException>();
        PolymorphicDeserializationFailures.Of(contexto.HttpContext).Should().BeNull();
    }

    [Fact(DisplayName = "Corpo válido é lido sem interferência do envoltório")]
    public async Task ReadAsync_CorpoValido_LeNormalmente()
    {
        InputFormatterContext contexto = Contexto<Comando>("""{"predicado":{"$tipo":"etapa","codigo":"PROVA_OBJETIVA"}}""");

        InputFormatterResult resultado = await Formatter().ReadAsync(contexto);

        resultado.HasError.Should().BeFalse();
        resultado.Model.Should().BeOfType<Comando>()
            .Which.Predicado.Should().BeOfType<Etapa>()
            .Which.Codigo.Should().Be("PROVA_OBJETIVA");
    }

    /// <summary>
    /// O ApiExplorer pergunta ao formatter quais media types ele consome, e é dessa resposta que
    /// sai o <c>requestBody</c> do OpenAPI. Envoltório que não delegue a pergunta esvazia o
    /// contrato publicado sem quebrar nenhuma requisição — o defeito passaria calado.
    /// </summary>
    [Fact(DisplayName = "Os content types consumidos continuam sendo anunciados")]
    public void GetSupportedContentTypes_Delega()
    {
        SystemTextJsonInputFormatter interno = FormatterDoFramework();

        new PolymorphicDeserializationInputFormatter(interno)
            .GetSupportedContentTypes(contentType: null, typeof(Comando))
            .Should().BeEquivalentTo(interno.GetSupportedContentTypes(contentType: null, typeof(Comando)));
    }

    private static PolymorphicDeserializationInputFormatter Formatter() =>
        new(FormatterDoFramework());

    private static SystemTextJsonInputFormatter FormatterDoFramework() =>
        new(
            new JsonOptions { JsonSerializerOptions = { PropertyNamingPolicy = JsonNamingPolicy.CamelCase } },
            NullLogger<SystemTextJsonInputFormatter>.Instance);

    private static InputFormatterContext Contexto<TModelo>(string corpo)
    {
        DefaultHttpContext http = new();
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(corpo));

        return new InputFormatterContext(
            http,
            modelName: string.Empty,
            new ModelStateDictionary(),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(TModelo)),
            static (stream, encoding) => new StreamReader(stream, encoding));
    }

    private sealed record Comando(Predicado Predicado, IReadOnlyList<Comando>? Itens = null);

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
    [JsonDerivedType(typeof(Etapa), "etapa")]
    private abstract record Predicado;

    private sealed record Etapa(string Codigo) : Predicado;

    private sealed record ComandoDeOutraUniao(Forma Forma);
    /// <summary>Tipo abstrato sem variantes declaradas — o desserializador não sabe construí-lo.</summary>
    private sealed record ComandoMalConfigurado(Contrato Valor);

    internal abstract record Contrato;


    [JsonPolymorphic(TypeDiscriminatorPropertyName = "discriminadorProprio")]
    [JsonDerivedType(typeof(Circulo), "circulo")]
    private abstract record Forma;

    private sealed record Circulo(int Raio) : Forma;
}

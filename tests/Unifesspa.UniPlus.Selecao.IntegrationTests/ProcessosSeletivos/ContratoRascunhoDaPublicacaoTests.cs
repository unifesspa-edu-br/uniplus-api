namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Xunit;

/// <summary>
/// O corpo do PUT do rascunho, lido como a porta HTTP o lê.
/// </summary>
/// <remarks>
/// Parâmetro de construtor de record não é exigido pelo System.Text.Json: sem
/// <c>[JsonRequired]</c>, o campo omitido chega com o valor default do tipo. Para
/// <c>JsonElement</c> esse default é <c>ValueKind.Undefined</c>, e a primeira coisa que o
/// handler faz com ele é <c>GetRawText()</c>, que lança — um corpo malformado viraria 500 em
/// vez do 400 que o contrato promete.
/// </remarks>
public sealed class ContratoRascunhoDaPublicacaoTests
{
    private static readonly JsonSerializerOptions Opcoes =
        new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Corpo sem conteúdo é recusado na leitura, e não no handler")]
    public void ConteudoAusente_ERecusadoNaDesserializacao()
    {
        Action ler = () => JsonSerializer.Deserialize<SalvarRascunhoDaPublicacaoRequest>(
            """{"versao":1}""", Opcoes);

        ler.Should().Throw<JsonException>(
            "a omissão precisa parar na porta: passar adiante um JsonElement Undefined faz o " +
            "handler estourar ao ler o texto cru, e o operador vê falha de servidor no lugar de " +
            "uma recusa que diz o que falta");
    }

    [Fact(DisplayName = "Corpo completo é lido sem interpretar o documento")]
    public void ConteudoPresente_ChegaOpaco()
    {
        SalvarRascunhoDaPublicacaoRequest? lido = JsonSerializer.Deserialize<SalvarRascunhoDaPublicacaoRequest>(
            """{"versao":3,"conteudo":{"ato":{"orgao":"REITORIA"},"numero":"07/2027"}}""", Opcoes);

        lido.Should().NotBeNull();
        lido!.Versao.Should().Be(3);
        lido.Conteudo.GetRawText().Should().Contain("REITORIA");
    }
}

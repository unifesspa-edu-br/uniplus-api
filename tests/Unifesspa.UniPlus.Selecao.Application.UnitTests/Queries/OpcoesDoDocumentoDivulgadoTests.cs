namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As opções do documento divulgado do certame: o enum com código usa o código declarado, e o enum sem código segue
/// pelo número, como o formato web sempre escreveu.
/// </summary>
public sealed class OpcoesDoDocumentoDivulgadoTests
{
    private sealed record Exemplo(ModoDeEntregaDeTeste Modo, SituacaoSemCodigoDeTeste Situacao);

    [Fact(DisplayName = "O enum com código sai e volta pelo código declarado, e o enum sem código sai pelo número")]
    public void Opcoes_EnumComCodigo_EscreveELeOCodigo()
    {
        Exemplo exemplo = new(ModoDeEntregaDeTeste.Postal, SituacaoSemCodigoDeTeste.Rascunho);

        string texto = JsonSerializer.Serialize(exemplo, ProjecaoDoCertamePublicado.OpcoesDoDocumento);

        texto.Should().Be("""{"modo":"pelo_correio","situacao":3}""");
        JsonSerializer.Deserialize<Exemplo>(texto, ProjecaoDoCertamePublicado.OpcoesDoDocumento).Should().Be(exemplo);
    }

    [Fact(DisplayName = "O código que o enum não declara é recusado sem repetir o valor recebido")]
    public void Opcoes_CodigoDesconhecido_RecusaComJsonException() =>
        FluentActions.Invoking(static () => JsonSerializer.Deserialize<Exemplo>(
                """{"modo":"codigo_inventado","situacao":3}""", ProjecaoDoCertamePublicado.OpcoesDoDocumento))
            .Should().Throw<JsonException>().Which.Message.Should().NotContain("codigo_inventado");

    [Fact(DisplayName = "As opções ficam somente leitura depois de criadas")]
    public void Opcoes_DepoisDeCriadas_FicamSomenteLeitura() =>
        ProjecaoDoCertamePublicado.OpcoesDoDocumento.IsReadOnly.Should().BeTrue();
}

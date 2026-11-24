namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As opções canônicas que alimentam o hash, o histórico da regra e a coluna do predicado: o enum com código usa o
/// código declarado, e o enum sem código segue pelo nome em camelCase, como sempre saiu.
/// </summary>
public sealed class OpcoesCanonicasDoHashTests
{
    private sealed record Exemplo(ModoDeEntregaDeTeste Modo, SituacaoSemCodigoDeTeste Situacao, ModoDeEntregaDeTeste? Opcional);

    [Fact(DisplayName = "O enum com código sai e volta pelo código declarado, e o enum sem código sai pelo nome")]
    public void Opcoes_EnumComCodigo_EscreveELeOCodigo()
    {
        Exemplo exemplo = new(ModoDeEntregaDeTeste.Postal, SituacaoSemCodigoDeTeste.Rascunho, Opcional: null);

        string texto = JsonSerializer.Serialize(exemplo, HashCanonicalComputer.CanonicalOptions);

        texto.Should().Be("""{"modo":"PELO_CORREIO","situacao":"rascunho"}""");
        JsonSerializer.Deserialize<Exemplo>(texto, HashCanonicalComputer.CanonicalOptions).Should().Be(exemplo);
    }

    [Fact(DisplayName = "O código que o enum não declara é recusado sem repetir o valor recebido")]
    public void Opcoes_CodigoDesconhecido_RecusaComJsonException() =>
        FluentActions.Invoking(static () => JsonSerializer.Deserialize<Exemplo>(
                """{"modo":"codigo_inventado","situacao":"rascunho"}""", HashCanonicalComputer.CanonicalOptions))
            .Should().Throw<JsonException>().Which.Message.Should().NotContain("codigo_inventado");

    [Fact(DisplayName = "As opções ficam somente leitura depois de criadas")]
    public void Opcoes_DepoisDeCriadas_FicamSomenteLeitura() =>
        HashCanonicalComputer.CanonicalOptions.IsReadOnly.Should().BeTrue();
}

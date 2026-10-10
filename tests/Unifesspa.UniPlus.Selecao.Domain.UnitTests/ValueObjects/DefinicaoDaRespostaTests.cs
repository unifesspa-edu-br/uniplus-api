namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinicaoDaRespostaTests
{
    [Theory(DisplayName = "A definição da resposta aceita só o valor que cabe na coluna do tipo")]
    [InlineData(TipoDoValorDeResposta.Data, false, "\"2001-03-09\"", true)]
    [InlineData(TipoDoValorDeResposta.Data, false, "\"09/03/2001\"", false)]
    [InlineData(TipoDoValorDeResposta.Numero, false, "18", true)]
    [InlineData(TipoDoValorDeResposta.Numero, false, "\"18\"", false)]
    [InlineData(TipoDoValorDeResposta.Codigo, true, "[\"LB_PPI\",\"AC\"]", true)]
    [InlineData(TipoDoValorDeResposta.Codigo, true, "\"LB_PPI\"", false)]
    [InlineData(TipoDoValorDeResposta.Codigo, false, "[\"LB_PPI\"]", false)]
    [InlineData(TipoDoValorDeResposta.Codigo, false, "\"\"", false)]
    [InlineData(TipoDoValorDeResposta.Endereco, false, "{\"cep\":\"68500000\",\"cidade\":{\"codigoIbge\":\"1504208\",\"nome\":\"Marabá\",\"uf\":\"PA\"}}", true)]
    [InlineData(TipoDoValorDeResposta.Endereco, false, "{\"cep\":\"68500000\",\"campoDesconhecido\":\"x\"}", false)]
    public void Aceita_SoValorDoTipo(TipoDoValorDeResposta tipo, bool multivalorado, string json, bool aceito)
    {
        DefinicaoDaResposta definicao = new(tipo, multivalorado, Identificador: false);

        definicao.Aceita(JsonSerializer.Deserialize<JsonElement>(json)).Should().Be(aceito);
    }

    [Fact(DisplayName = "Código acima do tamanho de uma opção é recusado; texto livre longo, não")]
    public void Codigo_AcimaDoTamanhoDeUmaOpcao_Recusado()
    {
        JsonElement longo = JsonSerializer.SerializeToElement(new string('A', 61));

        new DefinicaoDaResposta(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false).Aceita(longo).Should().BeFalse();
        new DefinicaoDaResposta(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false).Aceita(longo).Should().BeTrue();
    }

    [Fact(DisplayName = "O endereço lido volta na mesma forma que o formulário envia")]
    public void Endereco_IdaEVolta_PreservaComponentes()
    {
        JsonElement enviado = JsonSerializer.Deserialize<JsonElement>(
            "{\"cep\":\"68500000\",\"logradouro\":\"Folha 31\",\"cidade\":{\"codigoIbge\":\"1504208\",\"nome\":\"Marabá\",\"uf\":\"PA\"},\"origem\":\"geo-api\"}");

        EnderecoDeclarado.TentarLer(enviado, out EnderecoDeclarado endereco).Should().BeTrue();
        JsonElement devolvido = endereco.ParaJson();

        devolvido.GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
        devolvido.GetProperty("logradouro").GetString().Should().Be("Folha 31");
        devolvido.GetProperty("origem").GetString().Should().Be("geo-api");
    }
}

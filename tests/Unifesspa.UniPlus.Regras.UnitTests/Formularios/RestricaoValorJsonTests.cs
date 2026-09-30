namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class RestricaoValorJsonTests
{
    [Fact(DisplayName = "Cada tipo de restrição volta da forma gravada com os mesmos limites, grupos e fatos")]
    public void ParaJsonEDeJson_PreservamCadaTipo()
    {
        PredicadoDnf maiorDeIdade = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("IDADE", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!)]).Value!;
        RestricaoValor[] restricoes =
        [
            new FaixaNumerica(1.5m, null),
            new TamanhoTexto(null, 80),
            new OpcoesPermitidas([new OpcoesCondicionadas(maiorDeIdade, ["B", "A"]), new OpcoesCondicionadas(null, ["C"])]),
            new OpcoesDasRespostas(["OPCAO_CURSO_2", "OPCAO_CURSO_1"]),
        ];

        using JsonDocument gravado = JsonDocument.Parse(RestricaoValorJson.ParaJson(restricoes).ToJsonString());
        IReadOnlyList<RestricaoValor> lidas = RestricaoValorJson.ListaDeJson(gravado.RootElement).Value!;

        RestricaoValorJson.ParaJson(lidas).ToJsonString().Should().Be(gravado.RootElement.GetRawText());
        lidas[0].Should().Be(new FaixaNumerica(1.5m, null));
        ((OpcoesPermitidas)lidas[2]).Entradas[0].Quando!.FatosCitados.Should().Equal("IDADE");
        ((OpcoesDasRespostas)lidas[3]).Fatos.Should().Equal("OPCAO_CURSO_1", "OPCAO_CURSO_2");
    }

    [Theory(DisplayName = "Forma gravada que viola a restrição é recusada, nunca aceita em silêncio")]
    [InlineData("""{"tipo":"FAIXA_NUMERICA","minimo":null,"maximo":null}""", RestricaoValorErrorCodes.LimitesIncoerentes)]
    [InlineData("""{"tipo":"TAMANHO_TEXTO","minimo":1.5}""", RestricaoValorErrorCodes.FormaJsonInvalida)]
    [InlineData("""{"tipo":"OPCOES_PERMITIDAS","entradas":[]}""", RestricaoValorErrorCodes.OpcoesVazias)]
    [InlineData("""{"tipo":"OPCOES_DAS_RESPOSTAS","fatos":[]}""", RestricaoValorErrorCodes.FatosVazios)]
    [InlineData("""{"tipo":"REGEX"}""", RestricaoValorErrorCodes.FormaJsonInvalida)]
    public void DeJson_FormaInvalida_Recusa(string json, string codigo)
    {
        using JsonDocument documento = JsonDocument.Parse(json);

        Result<RestricaoValor> lida = RestricaoValorJson.DeJson(documento.RootElement);

        lida.Error!.Code.Should().Be(codigo);
    }
}

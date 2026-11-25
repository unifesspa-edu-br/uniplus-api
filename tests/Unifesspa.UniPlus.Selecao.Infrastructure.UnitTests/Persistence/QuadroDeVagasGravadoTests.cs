namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class QuadroDeVagasGravadoTests
{
    [Theory(DisplayName = "Variantes de ajuste preservam os parâmetros gravados")]
    [InlineData("""{"$tipo":"reduzirDe","modalidadeCodigo":"AC"}""")]
    [InlineData("""{"$tipo":"reduzirProporcionalEm","modalidadeCodigos":["AC","LI"]}""")]
    public void Ajuste_VarianteGravada_PreservaParametros(string json) => ConferirJson("AjusteDoQuadro", json);

    [Fact(DisplayName = "Retiradas preservam modalidades e quantidades gravadas")]
    public void Retiradas_Gravadas_PreservamModalidadesEQuantidades() =>
        ConferirJson("VagasRetiradasNoAjuste", """[{"modalidadeCodigo":"AC","quantidade":2},{"modalidadeCodigo":"LI","quantidade":1}]""");

    [Theory(DisplayName = "Ajuste recusa campo ausente, nulo, com caixa incorreta ou desconhecido")]
    [InlineData("""{"$tipo":"reduzirDe"}""")]
    [InlineData("""{"$tipo":"reduzirDe","modalidadeCodigo":null}""")]
    [InlineData("""{"$tipo":"reduzirDe","ModalidadeCodigo":"AC"}""")]
    [InlineData("""{"$tipo":"reduzirDe","modalidadeCodigo":"AC","extra":1}""")]
    public void Ajuste_ParametroInvalido_RecusaLeitura(string json) => RecusarJson("AjusteDoQuadro", json);

    [Theory(DisplayName = "Retiradas recusam campo ausente, nulo, com caixa incorreta ou desconhecido")]
    [InlineData("""[{"modalidadeCodigo":"AC"}]""")]
    [InlineData("""[{"modalidadeCodigo":null,"quantidade":2}]""")]
    [InlineData("""[{"ModalidadeCodigo":"AC","quantidade":2}]""")]
    [InlineData("""[{"modalidadeCodigo":"AC","quantidade":2,"extra":1}]""")]
    public void Retiradas_ParametroInvalido_RecusamLeitura(string json) => RecusarJson("VagasRetiradasNoAjuste", json);

    private static void ConferirJson(string propriedade, string json)
    {
        ValueConverter conversor = Conversor(propriedade);
        object valor = conversor.ConvertFromProvider(json)!;
        string gravado = (string)conversor.ConvertToProvider(valor)!;
        JsonNode.DeepEquals(JsonNode.Parse(gravado), JsonNode.Parse(json)).Should().BeTrue();
    }

    private static void RecusarJson(string propriedade, string json)
    {
        Action ler = () => Conversor(propriedade).ConvertFromProvider(json);
        ler.Should().Throw<JsonException>();
    }

    private static ValueConverter Conversor(string propriedade) =>
        ModeloDaSelecao.Propriedade(typeof(ConfiguracaoDistribuicaoVagas), propriedade).GetValueConverter()!;
}

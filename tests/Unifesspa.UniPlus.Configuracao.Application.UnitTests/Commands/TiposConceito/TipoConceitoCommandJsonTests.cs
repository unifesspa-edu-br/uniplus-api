namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.TiposConceito;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

/// <summary>
/// Se o valor conta como aprovação, omitido no JSON, desserializa como nulo, para a validação de domínio
/// recusá-lo no campo do valor: um bool ausente viraria <c>false</c> e o valor nasceria sem aprovar sem que
/// ninguém tivesse dito isso.
/// </summary>
public sealed class TipoConceitoCommandJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact(DisplayName = "Omitir contaComoAprovacao de um valor no JSON desserializa como nulo, não como false")]
    public void Valor_SemContaComoAprovacao_DesserializaComNulo()
    {
        const string json = """{ "codigo": "A", "rotulo": "Alfa" }""";

        ValorCategoricoCommand? valor = JsonSerializer.Deserialize<ValorCategoricoCommand>(json, Options);

        valor!.ContaComoAprovacao.Should().BeNull();
    }

    private const string CategoricoCompleto = """
        {
          "codigo": "PARECER",
          "definicao": {
            "nome": "Parecer", "natureza": "CATEGORICO", "numerico": null,
            "valores": [
              { "codigo": "B", "rotulo": "Beta", "contaComoAprovacao": false },
              { "codigo": "A", "rotulo": "Alfa", "contaComoAprovacao": true }
            ]
          }
        }
        """;

    [Fact(DisplayName = "JSON completo do tipo categórico desserializa na ordem dos valores")]
    public void Criar_Completo_Desserializa()
    {
        CriarTipoConceitoCommand? comando = JsonSerializer.Deserialize<CriarTipoConceitoCommand>(CategoricoCompleto, Options);

        comando!.Definicao!.Valores!.Select(v => v.Codigo).Should().Equal("B", "A");
        comando.Definicao.Valores![1].ContaComoAprovacao.Should().BeTrue();
    }

    [Fact(DisplayName = "JSON do tipo numérico desserializa a escala e deixa os valores nulos")]
    public void Criar_Numerico_Desserializa()
    {
        const string json = """
        { "codigo": "NOTA", "definicao": { "nome": "Nota", "natureza": "NUMERICO",
          "numerico": { "escalaMinima": 0, "escalaMaxima": 10, "casasDecimais": 2 }, "valores": null } }
        """;

        CriarTipoConceitoCommand? comando = JsonSerializer.Deserialize<CriarTipoConceitoCommand>(json, Options);

        comando!.Definicao!.Numerico.Should().Be(new EscalaNumericaCommand(0m, 10m, 2));
        comando.Definicao.Valores.Should().BeNull();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinicaoDaRespostaNomeCivilTests
{
    [Fact(DisplayName = "A resposta do NOME não carrega o formato congelado no formulário; a do nome da mãe carrega")]
    public void De_FormatoCongelado_SoValeForaDoNomeCivil()
    {
        DefinicaoDaResposta nome = DefinicaoDaResposta.De(TipoRenderizacao.Texto, "PESSOAL", "NOME_PESSOA", "NOME");
        DefinicaoDaResposta mae = DefinicaoDaResposta.De(TipoRenderizacao.Texto, "PESSOAL", "NOME_PESSOA", "NOME_MAE");

        nome.Formato.Should().BeNull();
        mae.Formato.Should().Be("NOME_PESSOA");
        nome.NaFormaCanonica(JsonSerializer.SerializeToElement("Maria S. Silva")).GetString().Should().Be("Maria S. Silva");
    }
}

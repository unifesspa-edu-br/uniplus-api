namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Qual resposta se grava cifrada: o número de documento (IDENTIFICADOR) e, de cifra individual, a
/// identidade de gênero e a orientação sexual (parecer da DPO de 13/10/2026) — nunca os demais fatos sensíveis.
/// </summary>
public sealed class DefinicaoDaRespostaCifraTests
{
    [Theory(DisplayName = "A identidade de gênero e a orientação sexual sensíveis têm cifra individual")]
    [InlineData("IDENTIDADE_GENERO")]
    [InlineData("ORIENTACAO_SEXUAL")]
    public void De_FatoComCifraIndividual_Cifra(string fato)
    {
        DefinicaoDaResposta definicao = DefinicaoDaResposta.De(TipoRenderizacao.SelecaoUnica, "SENSIVEL", fatoCodigo: fato);

        definicao.CifraIndividual.Should().BeTrue();
        definicao.Cifrada.Should().BeTrue();
        definicao.Identificador.Should().BeFalse("a leitura e o comprovante do titular devolvem o valor, ao contrário do número de documento");
    }

    [Theory(DisplayName = "O sexo, o sexo civil e os demais fatos sensíveis não têm cifra individual")]
    [InlineData("SEXO", "PESSOAL")]
    [InlineData("SEXO_CIVIL", "PESSOAL")]
    [InlineData("COR_RACA", "SENSIVEL")]
    [InlineData("TIPO_DEFICIENCIA", "SENSIVEL")]
    [InlineData("CONDICAO_ATENDIMENTO", "SENSIVEL")]
    public void De_DemaisFatos_NaoCifra(string fato, string classificacao)
    {
        DefinicaoDaResposta definicao = DefinicaoDaResposta.De(TipoRenderizacao.SelecaoUnica, classificacao, fatoCodigo: fato);

        definicao.CifraIndividual.Should().BeFalse();
        definicao.Cifrada.Should().BeFalse();
    }

    [Fact(DisplayName = "O número de documento segue cifrado e fora do comprovante")]
    public void De_Identificador_CifraEExcluiDoComprovante()
    {
        DefinicaoDaResposta definicao = DefinicaoDaResposta.De(TipoRenderizacao.Texto, "IDENTIFICADOR", fatoCodigo: "RG_NUMERO");

        definicao.Cifrada.Should().BeTrue();
        definicao.Identificador.Should().BeTrue();
        definicao.CifraIndividual.Should().BeFalse();
    }
}

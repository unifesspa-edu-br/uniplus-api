namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class ChavesDasBancasDoProcessoTests
{
    [Fact]
    public void Formar_ComChavesDasFasesEEtapas_ReservaOMenorSufixoLivre()
    {
        ChavesDasBancasDoProcesso chaves = new();
        chaves.Reservar([ChaveLocal.Criar("banca_entrevista").Value]);
        chaves.Reservar([ChaveLocal.Criar("banca_entrevista_2").Value]);

        chaves.Formar("ENTREVISTA").Valor.Should().Be("banca_entrevista_3");
    }

    [Theory]
    [InlineData("BANCA", "banca")]
    [InlineData("BANCA_HETEROIDENTIFICACAO", "banca_heteroidentificacao")]
    [InlineData("BANCARIA", "banca_bancaria")]
    public void Formar_ComPrefixoNoCodigo_NaoRepetePalavra(string codigo, string esperada)
    {
        ChavesDasBancasDoProcesso chaves = new();

        chaves.Formar(codigo).Valor.Should().Be(esperada);
        chaves.Formar(codigo).Valor.Should().Be(esperada + "_2");
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// As colunas de unidade dos dois prazos do recurso, na fase e na etapa: largura do código de contrato e, na 2ª instância,
/// aceite de nulo. Não abre conexão: só o modelo.
/// </summary>
public sealed class ColunasDeUnidadeDoPrazoDoRecursoTests
{
    private const int LarguraDoCodigoDeContrato = 30;

    [Theory(DisplayName = "A unidade do prazo do recurso cabe em um código de contrato")]
    [InlineData("recursos_configurados_da_fase", "prazo_unidade")]
    [InlineData("recursos_configurados_da_fase", "prazo_segunda_instancia_unidade")]
    [InlineData("recursos_configurados_da_etapa", "prazo_unidade")]
    [InlineData("recursos_configurados_da_etapa", "prazo_segunda_instancia_unidade")]
    public void Modelo_UnidadeDoPrazo_TemALarguraDoCodigoDeContrato(string tabela, string coluna) =>
        ModeloDaSelecao.Colunas(tabela).Single(c => c.Nome == coluna).LarguraMaxima.Should().Be(LarguraDoCodigoDeContrato);

    [Theory(DisplayName = "A unidade do prazo da 2ª instância aceita nulo, que é a ausência de instância superior")]
    [InlineData("recursos_configurados_da_fase")]
    [InlineData("recursos_configurados_da_etapa")]
    public void Modelo_UnidadeDaSegundaInstancia_AceitaNulo(string tabela) =>
        ModeloDaSelecao.Colunas(tabela).Single(c => c.Nome == "prazo_segunda_instancia_unidade").Anulavel.Should().BeTrue();
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class UnidadeResponsavelDaFaseConfigurationTests
{
    [Theory]
    [InlineData("unidade_responsavel_origem_id")]
    [InlineData("unidade_responsavel_codigo")]
    [InlineData("unidade_responsavel_sigla")]
    [InlineData("unidade_responsavel_nome")]
    public void UnidadeOpcional_Coluna_AceitaNuloETemComentario(string nome)
    {
        ColunaDoModelo coluna = ModeloDaSelecao.Colunas("fases_cronograma").Single(coluna => coluna.Nome == nome);

        coluna.Anulavel.Should().BeTrue();
        coluna.Comentario.Should().NotBeNullOrWhiteSpace();
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class CopiaDaOfertaDeCursoNoModeloTests
{
    [Theory]
    [InlineData("oferta_curso_codigo")]
    [InlineData("oferta_curso_nome")]
    [InlineData("oferta_curso_grau")]
    [InlineData("oferta_unidade_ofertante_nome")]
    [InlineData("oferta_turnos")]
    public void Modelo_ColunaDaOferta_AnulavelEComOrigem(string nome)
    {
        ColunaDoModelo coluna = ModeloDaSelecao.Colunas("configuracoes_de_vagas").Single(item => item.Nome == nome);
        coluna.Anulavel.Should().BeTrue();
        coluna.Comentario.Should().Contain("do cadastro na definição da distribuição e na publicação");
    }

    [Fact]
    public void Modelo_ConfiguracaoDeVagas_TabelaEColunasComentadas()
    {
        ModeloDaSelecao.ComentarioDaTabela(typeof(ConfiguracaoDistribuicaoVagas)).Should().NotBeNullOrWhiteSpace();
        ModeloDaSelecao.Colunas("configuracoes_de_vagas").Should().OnlyContain(coluna => !string.IsNullOrWhiteSpace(coluna.Comentario));
    }

    [Fact]
    public void Modelo_CodigoDaOferta_ConfereFormato()
    {
        string sql = ModeloDaSelecao.SqlDaRestricao(typeof(ConfiguracaoDistribuicaoVagas), "ck_configuracoes_de_vagas_oferta_curso_codigo_formato");
        sql.Should().Be($"oferta_curso_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
    }
}

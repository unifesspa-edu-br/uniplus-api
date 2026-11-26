namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// Como o modelo guarda a manifestação de interesse: uma só coluna anulável na linha do processo, com o código de contrato, e a
/// restrição que impede qualquer outro valor. Não abre conexão: só o modelo.
/// </summary>
public sealed class ManifestacaoDeInteresseNoModeloTests
{
    private const string Tabela = "processos_seletivos";

    [Fact(DisplayName = "A manifestação é uma só coluna anulável e comentada na linha do processo")]
    public void Coluna_UmaSoAnulavelEComentada()
    {
        IReadOnlyList<ColunaDoModelo> colunas = ModeloDaSelecao.Colunas(Tabela);

        colunas.Where(static c => c.Nome.Contains("manifestacao", StringComparison.Ordinal)).Should().ContainSingle()
            .Which.Should().Match<ColunaDoModelo>(static c => c.Nome == "manifestacao_de_interesse" && c.Anulavel && !string.IsNullOrWhiteSpace(c.Comentario));
    }

    [Fact(DisplayName = "O comentário da coluna diz que nulo é a manifestação não prevista e lista os códigos")]
    public void Coluna_ComentarioDizNuloENaoPrevista()
    {
        ColunaDoModelo coluna = ModeloDaSelecao.Colunas(Tabela).Single(static c => c.Nome == "manifestacao_de_interesse");

        coluna.Comentario.Should().Contain("Nulo = não prevista").And.Contain("UMA_VEZ").And.Contain("CONFIRMA_A_CADA_CHAMADA");
    }

    [Fact(DisplayName = "O banco recusa valor fora dos dois códigos e aceita o nulo")]
    public void Restricao_SoOsDoisCodigosOuNulo() =>
        ModeloDaSelecao.SqlDaRestricao(typeof(ProcessoSeletivo), "ck_processos_seletivos_manifestacao_de_interesse")
            .Should().Be("manifestacao_de_interesse IS NULL OR manifestacao_de_interesse IN ('UMA_VEZ', 'CONFIRMA_A_CADA_CHAMADA')");

    [Fact(DisplayName = "Não existem colunas separadas para manifestação prevista e confirmação a cada chamada")]
    public void Colunas_SemDeclaracoesSeparadas() =>
        ModeloDaSelecao.Colunas(Tabela).Select(static c => c.Nome)
            .Should().NotContain(["manifestacao_prevista", "confirmacao_a_cada_chamada"]);
}

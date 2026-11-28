namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// Como o modelo guarda a importação do resultado: a primeira fase e a marca de declaração na linha do processo e os relatórios em
/// tabela própria, um por leiaute. Não abre conexão: só o modelo.
/// </summary>
public sealed class ImportacaoDoResultadoNoModeloTests
{
    private const string Tabela = "relatorios_da_importacao";

    [Fact(DisplayName = "A primeira fase e a marca de declaração são colunas comentadas da linha do processo")]
    public void LinhaDoProcesso_TemFaseEMarca()
    {
        IReadOnlyList<ColunaDoModelo> colunas = ModeloDaSelecao.Colunas("processos_seletivos");

        colunas.Single(static c => c.Nome == "primeira_fase_depois_importacao_id").Should()
            .Match<ColunaDoModelo>(static c => c.Anulavel && !string.IsNullOrWhiteSpace(c.Comentario));
        colunas.Single(static c => c.Nome == "importacao_declarada").Anulavel.Should().BeTrue();
    }

    [Fact(DisplayName = "Os relatórios ficam em tabela própria, com leiaute, versão e posição obrigatórios")]
    public void TabelaDosRelatorios_ColunasObrigatorias()
    {
        IReadOnlyList<ColunaDoModelo> colunas = ModeloDaSelecao.Colunas(Tabela);

        colunas.Where(static c => c.Nome is "leiaute_codigo" or "leiaute_versao" or "posicao" or "ProcessoSeletivoId" or "processo_seletivo_id")
            .Should().OnlyContain(static c => !c.Anulavel && !string.IsNullOrWhiteSpace(c.Comentario));
        colunas.Select(static c => c.Nome).Should().Contain(["leiaute_codigo", "leiaute_versao", "posicao"]);
    }
}

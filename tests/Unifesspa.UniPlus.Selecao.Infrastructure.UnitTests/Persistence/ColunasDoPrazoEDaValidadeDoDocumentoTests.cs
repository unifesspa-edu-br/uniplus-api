namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// As colunas do prazo de emissão e da validade na tabela dos documentos exigidos: todas anuláveis, uma para cada
/// detalhe, e cada coluna com o comentário que a explica. Não abre conexão: só o modelo.
/// </summary>
public sealed class ColunasDoPrazoEDaValidadeDoDocumentoTests
{
    private const string Tabela = "documentos_exigidos";

    private static readonly string[] ColunasDoPrazoEDaValidade =
    [
        "prazo_emissao_escolha", "prazo_emissao_valor", "prazo_emissao_unidade", "prazo_emissao_referencia_tipo",
        "prazo_emissao_referencia_data", "prazo_emissao_referencia_fase_id", "prazo_emissao_efeito",
        "validade_escolha", "validade_referencia_tipo", "validade_referencia_data", "validade_referencia_fase_id", "validade_efeito",
    ];

    private static IReadOnlyList<ColunaDoModelo> Colunas => ModeloDaSelecao.Colunas(Tabela);

    [Fact(DisplayName = "A tabela tem uma coluna para cada detalhe do prazo de emissão e da validade, e nenhuma da idade máxima")]
    public void Modelo_TemAsColunasDoPrazoEDaValidade()
    {
        IEnumerable<string> nomes = Colunas.Select(static c => c.Nome);

        nomes.Should().Contain(ColunasDoPrazoEDaValidade);
        nomes.Should().NotContain(static n => n.StartsWith("idade_maxima", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "As colunas do prazo de emissão e da validade aceitam nulo, que é a escolha por fazer")]
    public void Modelo_ColunasDoPrazoEDaValidadeSaoAnulaveis() =>
        Colunas.Where(static c => ColunasDoPrazoEDaValidade.Contains(c.Nome))
            .Should().OnlyContain(static c => c.Anulavel);

    [Fact(DisplayName = "Cada coluna da tabela e a própria tabela têm comentário")]
    public void Modelo_TodaColunaETabelaTemComentario()
    {
        Colunas.Where(static c => string.IsNullOrWhiteSpace(c.Comentario)).Select(static c => c.Nome)
            .Should().BeEmpty();
        ModeloDaSelecao.ComentarioDaTabela(typeof(DocumentoExigido)).Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "O comentário da coluna de enum lista os códigos que ela aceita")]
    [InlineData("prazo_emissao_escolha", "COM_PRAZO")]
    [InlineData("prazo_emissao_unidade", "MESES")]
    [InlineData("prazo_emissao_referencia_tipo", "FIM_DAS_INSCRICOES")]
    [InlineData("prazo_emissao_efeito", "ACEITAR_COM_AVISO")]
    [InlineData("validade_escolha", "EXIGE_DOCUMENTO_VALIDO")]
    [InlineData("validade_referencia_tipo", "DATA_DA_APRESENTACAO")]
    [InlineData("validade_efeito", "RECUSAR_DOCUMENTO")]
    public void Modelo_ComentarioDaColunaDeEnum_ListaOsCodigos(string coluna, string codigo) =>
        Colunas.Single(c => c.Nome == coluna).Comentario.Should().Contain(codigo);
}

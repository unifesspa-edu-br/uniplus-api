namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using TestSupport;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

/// <summary>
/// O termo digitado em uma busca não chega ao log de requisição. A conferência lê os parâmetros de query string que
/// cada executável publica e reprova texto livre que o log não mascare, para que listagem nova não grave o termo.
/// </summary>
public sealed class ParametrosDeBuscaNoLogTests
{
    private static readonly string[] MascaradosPorPadrao = [.. RequestLoggingOptions.DefaultsNomesParametrosSensiveis()];

    [Fact(DisplayName = "Todo parâmetro de texto livre publicado é mascarado no log ou é exceção temporária")]
    public async Task ParametrosPublicados_TextoLivreEstaCoberto()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();

        string naoCobertos = string.Join("; ", RegraDosParametrosDeBusca.NaoCobertos(
            publicados.SelectMany(static documento => documento.Operacoes), MascaradosPorPadrao));

        naoCobertos.Should().BeEmpty("o termo de busca é texto livre e pode ser dado pessoal: inclua o nome em RequestLoggingOptions.DefaultsNomesParametrosSensiveis");
    }

    [Fact(DisplayName = "Os parâmetros de texto do cliente de hoje são lidos como texto livre e estão mascarados")]
    public async Task ParametrosDeBuscaConhecidos_SaoDetectadosEMascarados()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();

        string[] textoLivre = [.. publicados.SelectMany(static documento => documento.Operacoes)
            .SelectMany(static operacao => operacao.ParametrosDeConsulta).Where(static parametro => parametro.TextoLivre).Select(static parametro => parametro.Nome).Distinct()];

        string[] doCliente = ["q", "busca", "contaId", "ocorrencia"];
        textoLivre.Should().Contain(doCliente);
        MascaradosPorPadrao.Should().Contain(doCliente);
        RegraDosParametrosDeBusca.ExcecoesTemporariasDeTextoLivre.Should().NotContain(doCliente);
    }

    [Fact(DisplayName = "Toda exceção temporária de texto livre ainda existe no contrato")]
    public async Task ExcecoesTemporarias_AindaSaoPublicadas()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();
        string[] nomes = [.. publicados.SelectMany(static documento => documento.Operacoes)
            .SelectMany(static operacao => operacao.ParametrosDeConsulta).Select(static parametro => parametro.Nome).Distinct(StringComparer.OrdinalIgnoreCase)];

        RegraDosParametrosDeBusca.ExcecoesTemporariasDeTextoLivre.Should().BeSubsetOf(nomes, "uma exceção sem parâmetro correspondente é resto de regra antiga");
    }

    [Fact(DisplayName = "A regra reprova a listagem com parâmetro de texto livre sem cobertura e aponta a listagem")]
    public void Regra_ReprovaTextoLivreNaoCoberto()
    {
        OperacaoPublicada[] operacoes = [Listagem("pesquisa", textoLivre: true)];

        RegraDosParametrosDeBusca.NaoCobertos(operacoes, MascaradosPorPadrao)
            .Should().Equal("GET /api/selecao/coisas (selecao): parâmetro 'pesquisa'");
    }

    [Theory(DisplayName = "A regra aceita parâmetro mascarado em qualquer grafia, exceção temporária e valor controlado")]
    [InlineData("q", true)]
    [InlineData("BUSCA", true)]
    [InlineData("ContaId", true)]
    [InlineData("ocorrencia", true)]
    [InlineData("situacao", true)]
    [InlineData("limit", false)]
    public void Regra_AceitaParametroCoberto(string nome, bool textoLivre)
    {
        RegraDosParametrosDeBusca.NaoCobertos([Listagem(nome, textoLivre)], MascaradosPorPadrao).Should().BeEmpty();
    }

    private static OperacaoPublicada Listagem(string parametro, bool textoLivre) =>
        new("UniPlus", "selecao", "GET", "/api/selecao/coisas", "ListarCoisas", [new ParametroDeConsulta(parametro, textoLivre)]);
}

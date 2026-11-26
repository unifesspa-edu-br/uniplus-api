namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// O contrato publica um nome para cada operação, e o nome é único em todo o executável, entre todos os documentos que
/// ele serve (ADR-0030). A conferência lê os documentos que cada executável gera em execução.
/// </summary>
public sealed class NomesDasOperacoesTests
{
    [Fact(DisplayName = "Cada contrato versionado é servido por exatamente um executável")]
    public async Task Contratos_SaoServidosPorUmExecutavel()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();

        string[] naoConferidos = [.. OperacoesPublicadas.NomesDosContratos().Where(nome => publicados.Count(documento => documento.Nome == nome) != 1)];

        naoConferidos.Should().BeEmpty("um contrato sem executável, ou servido por dois, escaparia da conferência dos nomes");
    }

    [Fact(DisplayName = "Todo documento que um executável serve tem contrato versionado")]
    public async Task Documentos_ServidosTemContratoVersionado()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();
        IReadOnlyList<string> contratos = OperacoesPublicadas.NomesDosContratos();

        string[] semContrato = [.. publicados.Select(static documento => documento.Nome).Where(nome => !contratos.Contains(nome)).Distinct(StringComparer.Ordinal)];

        semContrato.Should().BeEmpty("um documento servido sem arquivo em contracts/ não teria o contrato aprovado nem revisto");
    }

    [Fact(DisplayName = "Toda operação publicada tem nome")]
    public async Task Operacoes_TemNome()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();

        RegraDosNomesDasOperacoes.SemNome(publicados.SelectMany(static documento => documento.Operacoes))
            .Should().BeEmpty("toda ação declara [EndpointName] (ADR-0030)");
    }

    [Fact(DisplayName = "Nenhum nome de operação se repete no mesmo executável")]
    public async Task Nomes_SaoUnicosNoExecutavel()
    {
        IReadOnlyList<DocumentoPublicado> publicados = await OperacoesPublicadas.LerAsync();

        RegraDosNomesDasOperacoes.Repetidos(publicados.SelectMany(static documento => documento.Operacoes))
            .Should().BeEmpty("o nome é único em todo o executável (ADR-0030)");
    }

    [Fact(DisplayName = "A regra reprova a operação sem nome e informa o verbo e o caminho")]
    public void Regra_ReprovaOperacaoSemNome()
    {
        OperacaoPublicada[] operacoes = [new("UniPlus", "selecao", "GET", "/api/selecao/coisas", null, []), new("UniPlus", "selecao", "POST", "/api/selecao/coisas", "CriarCoisa", [])];

        RegraDosNomesDasOperacoes.SemNome(operacoes).Should().Equal("GET /api/selecao/coisas (selecao)");
    }

    [Fact(DisplayName = "A regra reprova o nome repetido em operações diferentes e informa quais são")]
    public void Regra_ReprovaNomeRepetido()
    {
        OperacaoPublicada[] operacoes =
        [
            new("UniPlus", "selecao", "GET", "/api/selecao/coisas", "ListarCoisas", []),
            new("UniPlus", "configuracao", "GET", "/api/configuracao/coisas", "ListarCoisas", []),
        ];

        RegraDosNomesDasOperacoes.Repetidos(operacoes).Should().ContainSingle()
            .Which.Should().Contain("UniPlus GET /api/configuracao/coisas").And.Contain("UniPlus GET /api/selecao/coisas");
    }

    [Fact(DisplayName = "A regra não reprova o mesmo endpoint publicado em mais de um documento do executável")]
    public void Regra_AceitaOMesmoEndpointEmVariosDocumentos()
    {
        OperacaoPublicada[] operacoes =
        [
            new("UniPlus", "selecao", "GET", "/api/profile/me", "ConsultarUsuarioAutenticado", []),
            new("UniPlus", "ingresso", "GET", "/api/profile/me", "ConsultarUsuarioAutenticado", []),
        ];

        RegraDosNomesDasOperacoes.Repetidos(operacoes).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra não compara nomes de executáveis diferentes")]
    public void Regra_NaoComparaExecutaveisDiferentes()
    {
        OperacaoPublicada[] operacoes =
        [
            new("UniPlus", "selecao", "GET", "/api/selecao/coisas", "ListarCoisas", []),
            new("Portal", "portal", "GET", "/api/portal/coisas", "ListarCoisas", []),
        ];

        RegraDosNomesDasOperacoes.Repetidos(operacoes).Should().BeEmpty();
    }
}

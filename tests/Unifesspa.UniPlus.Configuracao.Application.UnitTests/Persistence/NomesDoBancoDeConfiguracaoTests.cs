namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Persistence;

using AwesomeAssertions;

public sealed class NomesDoBancoDeConfiguracaoTests
{
    private readonly NomesDoBanco _nomes = new("configuracao");

    [Fact]
    public void Indices_Prefixo_DeveConterOTipoEATabela()
    {
        string[] infratores = _nomes.IndicesComPrefixoInvalido();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }

    [Fact]
    public void Restricoes_Prefixo_DeveConterOTipoEATabela()
    {
        string[] infratores = _nomes.RestricoesComPrefixoInvalido();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }

    [Fact]
    public void Nomes_Sufixo_NaoDevemTerminarEmVivo()
    {
        string[] infratores = _nomes.NomesTerminadosEmVivo();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }

    [Fact]
    public void Nomes_Tamanho_NaoDeveExceder63Bytes()
    {
        string[] infratores = _nomes.NomesLongos();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }

    [Fact]
    public void Tabelas_Tamanho_NaoDeveExceder30Caracteres()
    {
        string[] infratores = _nomes.TabelasLongas();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }
}

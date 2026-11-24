namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

public sealed class NomesDoBancoDoMonolitoTests
{
    private readonly NomesDoBanco _nomes = new();

    [Fact]
    public void Modelo_ComTabelas_DeveEncontrarTabelas() => _nomes.Tabelas().Should().NotBeEmpty();

    [Fact]
    public void Nomes_Prefixo_DeveConterOTipoEATabela()
    {
        string[] infratores = _nomes.PrefixosInvalidos();
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
        string[] infratores = _nomes.Tabelas().Where(nome => nome.Length > 30).ToArray();
        infratores.Should().BeEmpty("nomes infratores: {0}", string.Join(", ", infratores));
    }
}

namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class ScriptsSqlDoModuloTests
{
    private static readonly Assembly AssemblyDoCore = typeof(UniPlusDbContext).Assembly;

    [Fact(DisplayName = "O conjunto compartilhado da plataforma embute a extensão e a função de normalização, nessa ordem, todos como Pre")]
    public async Task Descobrir_ConjuntoCompartilhado_TemOsDoisScriptsPreEmOrdem()
    {
        ScriptsSqlDoModulo scripts = await ScriptsSqlDoModulo.DescobrirAsync(AssemblyDoCore, CancellationToken.None);

        scripts.Pre.Select(static s => s.Nome).Should().HaveCount(2)
            .And.ContainInConsecutiveOrder(
                "Unifesspa.UniPlus.Infrastructure.Core.Persistence.Scripts.Pre.001_extensao_btree_gist.sql",
                "Unifesspa.UniPlus.Infrastructure.Core.Persistence.Scripts.Pre.002_normalizar_para_comparacao.sql");
        scripts.Pos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os scripts embutidos trazem o texto do arquivo, para a função e a extensão compartilhadas")]
    public async Task Descobrir_ConjuntoCompartilhado_LeOTextoDosScripts()
    {
        ScriptsSqlDoModulo scripts = await ScriptsSqlDoModulo.DescobrirAsync(AssemblyDoCore, CancellationToken.None);

        scripts.Pre.Select(static s => s.Texto).Should().Contain(t => t.Contains("CREATE EXTENSION IF NOT EXISTS btree_gist", StringComparison.Ordinal))
            .And.Contain(t => t.Contains("plataforma.normalizar_para_comparacao", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Um assembly sem script devolve os dois grupos vazios")]
    public async Task Descobrir_AssemblySemScript_DevolveVazio()
    {
        ScriptsSqlDoModulo scripts = await ScriptsSqlDoModulo.DescobrirAsync(typeof(string).Assembly, CancellationToken.None);

        scripts.Pre.Should().BeEmpty();
        scripts.Pos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A lista de assemblies dos scripts começa pela plataforma e não repete o assembly de contribuições do mesmo projeto")]
    public void Assemblies_ComecaPelaPlataformaESemRepetirAssembly()
    {
        IContribuicaoDoModulo[] modulos =
        [
            new ModuloDeTeste<EntidadeDoModuloA>("modulo_a"),
            new ModuloDeTeste<EntidadeDoModuloB>("modulo_b"),
        ];

        IReadOnlyList<Assembly> assemblies = ScriptsDasContribuicoes.Assemblies(modulos);

        assemblies.Should().Equal(AssemblyDoCore, typeof(ModuloDeTeste<EntidadeDoModuloA>).Assembly);
    }
}

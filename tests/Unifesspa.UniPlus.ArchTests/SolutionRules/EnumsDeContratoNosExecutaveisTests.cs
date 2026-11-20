namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Infrastructure;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A conferência dos códigos de contrato na subida, sobre o catálogo real de cada executável (ADR-0176): o alcance de
/// cada um chega aos projetos que ele usa, só traz projetos do Uni+, e a conferência de todos eles não encontra defeito.
/// Quem barra o defeito no CI é este teste; o host de teste do Host e do Portal também o executa ao iniciar.
/// </summary>
public sealed class EnumsDeContratoNosExecutaveisTests
{
    private const string PrefixoDoUniPlus = "Unifesspa.UniPlus.";

    private static readonly Assembly Kernel = typeof(AssembliesDoUniPlus).Assembly;

    private static readonly Assembly InfrastructureCore = typeof(ConferenciaDosEnumsDeContratoNaSubida).Assembly;

    // O Portal é um deploy à parte: o Host não o compõe.
    private static readonly string[] ModulosDoHost = [.. ModulosDoMonolito.Nomes.Where(static nome => nome != "Portal")];

    private static readonly string[] CamadasDeCadaModulo = ["API", "Domain", "Infrastructure"];

    private static readonly string[] NomesDosAssembliesDosModulosDoHost =
        [.. ModulosDoHost.SelectMany(static modulo => CamadasDeCadaModulo.Select(camada => $"{PrefixoDoUniPlus}{modulo}.{camada}"))];

    public static TheoryData<Assembly> Executaveis => new(ExecutaveisDoProduto.Todos);

    [Theory(DisplayName = "A conferência dos enums do catálogo real não encontra defeito, a partir de cada executável")]
    [MemberData(nameof(Executaveis))]
    public void ConferirNoExecutavel_CatalogoReal_NaoLanca(Assembly executavel) =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirNoExecutavel(executavel)).Should().NotThrow();

    [Theory(DisplayName = "Fora o executável, o alcance só traz assemblies cujo nome começa com o prefixo do Uni+")]
    [MemberData(nameof(Executaveis))]
    public void AlcancadosAPartirDe_ForaOExecutavel_SoTrazAssembliesDoUniPlus(Assembly executavel) =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(executavel).Where(a => a != executavel).Select(static a => a.GetName().Name)
            .Should().OnlyContain(static nome => nome != null && nome.StartsWith(PrefixoDoUniPlus, StringComparison.Ordinal));

    [Fact(DisplayName = "O alcance do Host traz o Kernel, as Regras, a Infrastructure.Core, a persistência e a API, o Domain e a Infrastructure de cada módulo")]
    public void AlcancadosAPartirDe_Host_TrazAsBibliotecasEOsModulos()
    {
        Assembly[] modulos = [.. AssembliesDoProduto.Todos.Where(static a => NomesDosAssembliesDosModulosDoHost.Contains(a.GetName().Name))];
        Assembly[] bibliotecas = [Kernel, typeof(EntradaDeRegras).Assembly, InfrastructureCore, typeof(MonolitoDbContext).Assembly];

        modulos.Should().HaveCount(NomesDosAssembliesDosModulosDoHost.Length, "cada módulo do Host tem API, Domain e Infrastructure no inventário do produto");
        AssembliesDoUniPlus.AlcancadosAPartirDe(ExecutaveisDoProduto.AssemblyDoHost).Should().Contain([.. bibliotecas, .. modulos]);
    }

    [Fact(DisplayName = "O alcance do Portal traz o Kernel, a Infrastructure.Core e os projetos de aplicação e infraestrutura dele")]
    public void AlcancadosAPartirDe_Portal_TrazAsBibliotecasEOsProjetosDele() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(ExecutaveisDoProduto.AssemblyDoPortal).Should().Contain(
        [
            Kernel,
            InfrastructureCore,
            typeof(ComposicaoDaPaginaDoCertame).Assembly,
            typeof(PortalInfrastructureRegistration).Assembly,
        ]);

    [Fact(DisplayName = "O alcance do Verificador traz o Kernel e os contratos e não traz a Infrastructure.Core nem a Seleção")]
    public void AlcancadosAPartirDe_Verificador_NaoAlcancaAInfraestruturaDaApiNemASelecao()
    {
        IReadOnlyList<Assembly> alcancados = AssembliesDoUniPlus.AlcancadosAPartirDe(ExecutaveisDoProduto.AssemblyDoVerificador);

        alcancados.Should().Contain([Kernel, typeof(DestinoDoResultado).Assembly]);
        alcancados.Should().NotContain(InfrastructureCore);
        alcancados.Where(static a => a.GetName().Name?.StartsWith($"{PrefixoDoUniPlus}Selecao", StringComparison.Ordinal) == true).Should().BeEmpty();
    }
}

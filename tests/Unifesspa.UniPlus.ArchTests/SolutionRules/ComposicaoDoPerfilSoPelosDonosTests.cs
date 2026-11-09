namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// A composição de um perfil comum vive na versão, e o acesso se decide pela versão a que a concessão está presa. Só
/// os donos da composição leem as permissões do perfil ou da versão, ou a versão vigente do perfil: quem lê por fora
/// decidiria acesso pela composição de hoje e não pela da concessão. A regra enxerga dependência e chamada entre
/// tipos; SQL bruto e texto de consulta não entram nela. Os tipos aninhados (máquinas de estado e lambdas) contam como
/// o tipo que os contém.
/// </summary>
public sealed class ComposicaoDoPerfilSoPelosDonosTests
{
    private const string Fixtures = @"^Unifesspa\.UniPlus\.ArchTests\.Fixtures(\.|$)";

    private const string Dominio = @"Unifesspa\.UniPlus\.Autorizacao\.Domain\.Entities\.";

    private const string Persistencia = @"Unifesspa\.UniPlus\.Autorizacao\.Infrastructure\.Persistence\.";

    // Dono das linhas de permissão: o perfil, a versão, o mapeamento do EF e as leituras do banco que as juntam.
    private const string DonosDasPermissoes =
        $@"^({Dominio}(Perfil|VersaoDoPerfil|PermissaoDoPerfil|PermissaoDaVersao)"
        + $@"|{Persistencia}(AutorizacaoDbContext|Configurations\.(PerfilConfiguration|VersaoDoPerfilConfiguration)"
        + $@"|Repositories\.(ComposicaoEmVigorNoBanco|ConsultasDePerfil))"
        + $@"|{Persistencia}Migrations\..+)(\+|$)";

    // Dono da leitura da composição em vigor: o perfil, quem presta a concessão e o pedido à versão, e a projeção.
    private const string DonosDaVersaoVigente =
        $@"^({Dominio}(Perfil|Concessao|PedidoDeConcessao)"
        + @"|Unifesspa\.UniPlus\.Autorizacao\.Application\.Mappings\.(PerfilMapping|VersaoMapping))(\+|$)";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(Assemblies()).Build();

    [Fact(DisplayName = "Fitness_LeituraDaComposicaoPorFora_Falha")]
    public void Fitness_LeituraDaComposicaoPorFora_Falha()
    {
        string[] fora = [.. Violacoes(Fixtures, semDono: true)];

        fora.Should().Contain(static v => v.Contains(nameof(FixturesDeComposicaoDoPerfil.DependeDaPermissaoDoPerfil), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDeComposicaoDoPerfil.DependeDaPermissaoDaVersao), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDeComposicaoDoPerfil.LeAVersaoVigenteDoPerfil), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDeComposicaoDoPerfil.LeAComposicaoEmVigorDoPerfil), StringComparison.Ordinal));
        fora.Should().NotContain(static v => v.Contains(nameof(FixturesDeComposicaoDoPerfil.LeSoONomeDoPerfil), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Fitness_SoOsDonosLeemAComposicaoDoPerfil_NoProduto")]
    public void Fitness_SoOsDonosLeemAComposicaoDoPerfil_NoProduto()
    {
        Types().That().DependOnAny(typeof(PermissaoDaVersao)).GetObjects(Arquitetura)
            .Should().NotBeEmpty("sem dependente descoberto, a conferência passaria vazia");

        Violacoes(@"^Unifesspa\.UniPlus\.(?!ArchTests)", semDono: false).Should().BeEmpty(
            "só os donos leem as permissões do perfil e da versão e a versão vigente do perfil");
    }

    [Fact(DisplayName = "Fitness_AutorizacaoCompartilhadaNaoDependeDoModuloDeAutorizacao")]
    public void Fitness_AutorizacaoCompartilhadaNaoDependeDoModuloDeAutorizacao()
    {
        string[] compartilhados = ["Unifesspa.UniPlus.Authorization", "Unifesspa.UniPlus.Infrastructure.Core"];

        string[] dependentes =
        [
            .. compartilhados
                .SelectMany(static n => ReflectionAssembly.Load(n).GetReferencedAssemblies())
                .Select(static a => a.Name ?? string.Empty)
                .Where(static n => n.StartsWith("Unifesspa.UniPlus.Autorizacao", StringComparison.Ordinal)),
        ];

        dependentes.Should().BeEmpty("o que decide acesso no ponto único não conhece o módulo que guarda os perfis");
    }

    private static List<string> Violacoes(string escopo, bool semDono)
    {
        List<string> violacoes = [];
        string donos = semDono ? @"^$" : DonosDasPermissoes;
        string donosDaVigente = semDono ? @"^$" : DonosDaVersaoVigente;

        Avaliar(
            Types().That().ResideInNamespaceMatching(escopo).And().DoNotHaveFullNameMatching(donos).Should()
                .NotDependOnAnyTypesThat().Are(Types().That().HaveFullName(typeof(PermissaoDoPerfil).FullName!)
                    .Or().HaveFullName(typeof(PermissaoDaVersao).FullName!)),
            violacoes);

        foreach (string leitura in new[] { "get_VersaoVigente(", "get_ComposicaoEmVigor(" })
        {
            Avaliar(
                Types().That().ResideInNamespaceMatching(escopo).And().DoNotHaveFullNameMatching(donosDaVigente).Should()
                    .NotCallAny(MethodMembers().That().AreDeclaredIn(typeof(Perfil)).And().HaveNameStartingWith(leitura)),
                violacoes);
        }

        return violacoes;
    }

    private static void Avaliar(TypesShouldConjunction regra, List<string> violacoes) =>
        violacoes.AddRange(regra.Evaluate(Arquitetura).Where(static r => !r.Passed).Select(static r => r.Description));

    private static ReflectionAssembly[] Assemblies() =>
        [
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal)
                    || Path.GetFileName(c) == "Unifesspa.UniPlus.ArchTests.dll")
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}

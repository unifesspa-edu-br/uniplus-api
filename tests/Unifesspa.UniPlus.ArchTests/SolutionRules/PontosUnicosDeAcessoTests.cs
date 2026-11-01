namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Cada decisão tem um ponto único de entrada (ADR-0012): só o verificador decide pelo serviço de decisão, só o acesso
/// do módulo monta o resource da Seleção, e só os endpoints técnicos fora da ADR-0036 mapeiam rota sem controller. O
/// escopo são os assemblies de produto; os testes, que usam o serviço de decisão, ficam fora. Os tipos aninhados (a
/// máquina de estados de cada método assíncrono e as classes das lambdas) contam como o tipo que os contém.
/// </summary>
public sealed class PontosUnicosDeAcessoTests
{
    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(AssembliesDoProduto()).Build();

    [Fact(DisplayName = "Só o verificador e a composição de DI dependem da interface do serviço de decisão")]
    public void ServicoDeDecisao_SoOVerificadorEAComposicaoDependem()
    {
        IEnumerable<IType> dependentes = Types().That().DependOnAny(typeof(IAuthorizationDecisionService)).GetObjects(Arquitetura);
        dependentes.Should().NotBeEmpty("sem dependente descoberto, a conferência passaria vazia");

        string[] fora =
        [
            .. dependentes
                .Select(static t => t.FullName)
                .Where(static n => !Regex.IsMatch(
                    n,
                    @"^Unifesspa\.UniPlus\.(Infrastructure\.Core\.Authorization\.(VerificadorDeAcessoHttp|AutorizacaoServiceCollectionExtensions)|Authorization\.Decisao\.AuthorizationDecisionService)(\+|$)")),
        ];
        fora.Should().BeEmpty("a decisão passa pelo verificador. Dependentes fora do ponto único:\n" + string.Join("\n", fora));
    }

    [Fact(DisplayName = "Na Seleção, só AcessoAoProcesso e AcessoAInscricao chamam ResourceContext.From")]
    public void SelecaoMontaResourceSoNoAcesso()
    {
        const string Selecao = @"^Unifesspa\.UniPlus\.Selecao(\.|$)";
        Types().That().ResideInNamespaceMatching(Selecao).And().CallAny(MethodMembers().That().HaveNameStartingWith("From(").And().AreDeclaredIn(typeof(ResourceContext)))
            .GetObjects(Arquitetura).Should().NotBeEmpty("as fábricas do acesso chamam ResourceContext.From");

        Violacoes(
            Types().That().ResideInNamespaceMatching(Selecao).And().DoNotHaveFullNameMatching(@"\.(AcessoAoProcesso|AcessoAInscricao)(\+|$)")
                .Should().NotCallAny(MethodMembers().That().HaveNameStartingWith("From(").And().AreDeclaredIn(typeof(ResourceContext))))
            .Should().BeEmpty("a Seleção monta o resource pelas fábricas do acesso");
    }

    [Fact(DisplayName = "Só os endpoints técnicos autorizados mapeiam rota por EndpointRouteBuilderExtensions")]
    public void RotasSemControllerSoEmEndpointsTecnicos()
    {
        Types().That().CallAny(MapDeRota()).GetObjects(Arquitetura).Select(static t => t.FullName).Should().Contain(static n => n.EndsWith("Profile.ProfileEndpointsExtensions", StringComparison.Ordinal), "os endpoints técnicos chamam Map*");

        // Um endpoint técnico novo que atenda à ADR-0036 entra nesta lista, no mesmo PR, com a justificativa.
        Violacoes(
            Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.").And().DoNotHaveFullNameMatching(
                    @"^Unifesspa\.UniPlus\.Infrastructure\.Core\.(HealthChecks\.HealthCheckEndpointsExtensions|Profile\.ProfileEndpointsExtensions)(\+|$)")
                .Should().NotCallAny(MapDeRota()))
            .Should().BeEmpty("rota de negócio é controller MVC (ADR-0036)");
    }

    // A busca por chamada de método não enxerga as chamadas do Program, que usa instruções de nível superior. É nele que um
    // app.MapGet novo apareceria, então o arquivo é varrido por texto, inclusive a chamada estática da extensão.
    [Fact(DisplayName = "O Program do host não mapeia rota sem controller")]
    public void ProgramNaoMapeiaRota()
    {
        string programa = File.ReadAllText(Path.Join(SolutionRootLocator.Locate(), "src", "host", "Unifesspa.UniPlus.Host", "Program.cs"));

        Regex.Matches(programa, @"(\.Map(Get|Post|Put|Delete|Patch|Methods|Group|Fallback)?|EndpointRouteBuilderExtensions\s*\.\s*Map\w*)\s*\(").Should().BeEmpty(
            "rota de negócio é controller MVC (ADR-0036); o Program usa MapControllers e as extensões dos endpoints técnicos");
    }

    private static ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers.GivenMethodMembersConjunction MapDeRota() =>
        MethodMembers().That().HaveNameStartingWith("Map(")
            .Or().HaveNameStartingWith("MapGet(")
            .Or().HaveNameStartingWith("MapPost(").Or().HaveNameStartingWith("MapPut(").Or().HaveNameStartingWith("MapDelete(")
            .Or().HaveNameStartingWith("MapPatch(").Or().HaveNameStartingWith("MapMethods(").Or().HaveNameStartingWith("MapGroup(")
            .Or().HaveNameStartingWith("MapFallback(");

    private static List<string> Violacoes(TypesShouldConjunction regra) =>
        [.. regra.Evaluate(Arquitetura).Where(static r => !r.Passed).Select(static r => r.Description)];

    private static ReflectionAssembly[] AssembliesDoProduto() =>
        [
            typeof(EndpointRouteBuilderExtensions).Assembly,
            ReflectionAssembly.Load("Microsoft.AspNetCore.Http.Extensions"),
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal))
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}

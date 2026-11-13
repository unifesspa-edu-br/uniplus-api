namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;

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
                .Where(static n => !PermitidoNaDecisao(n)),
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

    private static bool PermitidoNaDecisao(string nome) =>
        nome == "Unifesspa.UniPlus.Authorization.Decisao.AuthorizationDecisionService"
        || nome.StartsWith("Unifesspa.UniPlus.Authorization.Decisao.AuthorizationDecisionService+", StringComparison.Ordinal)
        || new[]
        {
            "Unifesspa.UniPlus.Infrastructure.Core.Authorization.VerificadorDeAcessoHttp",
            "Unifesspa.UniPlus.Infrastructure.Core.Authorization.AutorizacaoServiceCollectionExtensions",
        }.Any(tipo => nome == tipo || nome.StartsWith(tipo + "+", StringComparison.Ordinal));

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

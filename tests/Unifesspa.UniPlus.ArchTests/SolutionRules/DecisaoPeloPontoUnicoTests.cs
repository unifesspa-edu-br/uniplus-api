namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Security.Claims;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Toda decisão de acesso passa pelo ponto único (ADR-0078): o módulo pergunta a
/// <c>IVerificadorDeAcesso</c>, que monta o sujeito explícito e chama o serviço de decisão.
/// Ler papel ou claim do principal para decidir, ou chamar o serviço sem a porta, é decidir
/// fora dele.
/// </summary>
public sealed class DecisaoPeloPontoUnicoTests
{
    private const string Fixtures = @"^Unifesspa\.UniPlus\.ArchTests\.Fixtures(\.|$)";

    // O ponto de decisão e a infraestrutura que lê o token. O Profile só devolve ao
    // próprio usuário os papéis do token, para exibição; não decide nada com eles.
    private const string Infraestrutura =
        @"^Unifesspa\.UniPlus\.(Authorization|Infrastructure\.Core\.(Authorization|Authentication|Profile))(\.|$)";

    private const string PontoDeDecisao =
        @"^Unifesspa\.UniPlus\.(Authorization|Infrastructure\.Core\.Authorization)(\.|$)";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(Assemblies()).Build();

    [Fact(DisplayName = "Fitness_DecisaoForaDoPontoUnicoOuSemSujeito_Falha")]
    public void Fitness_DecisaoForaDoPontoUnicoOuSemSujeito_Falha()
    {
        IReadOnlyList<string> fora = Violacoes(Fixtures);

        fora.Should().Contain(static v => v.Contains(nameof(FixturesDePontoUnico.DecidePeloPapelDoPrincipal), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDePontoUnico.DecidePelaClaimDoPrincipal), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDePontoUnico.DecidePeloPapelDoContexto), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains(nameof(FixturesDePontoUnico.ChamaOServicoDeDecisaoSemPassarPelaPorta), StringComparison.Ordinal));
        fora.Should().NotContain(static v => v.Contains(nameof(FixturesDePontoUnico.DecidePelaPortaDoPontoUnico), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Fitness_DecisaoPeloPontoUnico_Passa")]
    public void Fitness_DecisaoPeloPontoUnico_Passa()
    {
        Violacoes(Fixtures).Where(static v => v.Contains(nameof(FixturesDePontoUnico.DecidePelaPortaDoPontoUnico), StringComparison.Ordinal))
            .Should().BeEmpty();

        IEnumerable<IType> consumidores = Types()
            .That().DoNotResideInNamespaceMatching(Infraestrutura)
            .And().DoNotResideInNamespaceMatching(Fixtures)
            .And().DependOnAny(Types().That().HaveFullName("Unifesspa.UniPlus.Authorization.Abstractions.IVerificadorDeAcesso"))
            .GetObjects(Arquitetura);
        consumidores.Should().NotBeEmpty("há módulos que decidem pela porta do ponto único, e eles não violam a regra");
    }

    [Fact(DisplayName = "Fitness_DecisaoDeNegocioPassaPeloPontoUnico_NoProduto")]
    public void Fitness_DecisaoDeNegocioPassaPeloPontoUnico_NoProduto()
    {
        Violacoes(@"^Unifesspa\.UniPlus\.(?!ArchTests)").Should().BeEmpty(
            "fora do ponto de decisão e da leitura do token, o módulo pergunta a IVerificadorDeAcesso e não lê papel nem claim do principal");
    }

    private static List<string> Violacoes(string escopo)
    {
        List<string> violacoes = [];

        Verificar(
            Types().That().ResideInNamespaceMatching(escopo).And().DoNotResideInNamespaceMatching(Infraestrutura).Should()
                .NotCallAny(MethodMembers().That().HaveNameStartingWith("IsInRole(")
                    .Or().HaveNameStartingWith("HasClaim(")
                    .Or().HaveNameStartingWith("HasRole(")
                    .Or().HaveNameStartingWith("get_Roles(")
                    .Or().HaveNameStartingWith("get_IsPlataformaAdmin(")
                    .Or().HaveNameStartingWith("GetResourceRoles(")),
            violacoes);

        Verificar(
            Types().That().ResideInNamespaceMatching(escopo).And().DoNotResideInNamespaceMatching(PontoDeDecisao).Should()
                .NotDependOnAnyTypesThat().HaveFullName("Unifesspa.UniPlus.Authorization.Abstractions.IAuthorizationDecisionService")
                .AndShould().NotDependOnAnyTypesThat().HaveFullName("Unifesspa.UniPlus.Authorization.Abstractions.IAuthorizationSubjectResolver"),
            violacoes);

        return violacoes;
    }

    private static void Verificar(TypesShouldConjunction regra, List<string> violacoes)
        => violacoes.AddRange(regra.Evaluate(Arquitetura).Where(static r => !r.Passed).Select(static r => r.Description));

    private static ReflectionAssembly[] Assemblies()
        =>
        [
            // O ArchUnitNET só enxerga a chamada a um membro cujo assembly foi carregado.
            typeof(ClaimsPrincipal).Assembly,
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal)
                    || Path.GetFileName(c) == "Unifesspa.UniPlus.ArchTests.dll")
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}

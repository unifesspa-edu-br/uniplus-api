namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// A titularidade só existe no registro de acesso (ADR-0166): o verificador de acesso monta a concessão efetiva de
/// titularidade e a decisão permitida que a carrega, para registrar a leitura que a parte faz do que é seu, e nada disso
/// chega ao ponto de decisão. A fonte é um valor de enum, que o compilador grava como número e a análise de dependência
/// não enxerga; por isso a regra restringe quem pode montar a concessão efetiva de titularidade
/// (<see cref="EffectiveGrant.PorTitularidade"/>): fora o verificador, só a própria concessão efetiva. A decisão permitida, por sua vez, só sai do serviço de decisão e do verificador.
/// </summary>
public sealed class TitularidadeSoNoRegistroTests
{
    private const string Fixtures = @"^Unifesspa\.UniPlus\.ArchTests\.Fixtures(\.|$)";

    private const string Produto = @"^Unifesspa\.UniPlus\.(?!ArchTests)";

    private const string Verificador = @"Infrastructure\.Core\.Authorization\.VerificadorDeAcessoHttp";

    private const string PontoDeDecisao = @"^Unifesspa\.UniPlus\.Authorization\.Decisao(\.|$)";

    private const string ResolvedorDoSujeito = @"^Unifesspa\.UniPlus\.Infrastructure\.Core\.Authorization\.HttpAuthorizationSubjectResolver(\+|$)";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(Assemblies()).Build();

    [Fact(DisplayName = "Fitness_TitularidadeOuDecisaoPermitidaForaDoVerificador_Falha")]
    public void Fitness_TitularidadeOuDecisaoPermitidaForaDoVerificador_Falha() =>
        Violacoes(Fixtures).Should()
            .Contain(static violacao => violacao.Contains(nameof(FixturesDeTitularidade.MontaTitularidade), StringComparison.Ordinal))
            .And.Contain(static violacao => violacao.Contains(nameof(FixturesDeTitularidade.PermiteForaDoPontoDeDecisao), StringComparison.Ordinal));

    [Fact(DisplayName = "Fitness_TitularidadeEDecisaoPermitidaSoNoVerificador_NoProduto")]
    public void Fitness_TitularidadeEDecisaoPermitidaSoNoVerificador_NoProduto() =>
        Violacoes(Produto).Should().BeEmpty(
            "só o verificador e a própria concessão efetiva montam a titularidade, e só o serviço de decisão e o verificador permitem");

    [Fact(DisplayName = "O ponto de decisão e a montagem das concessões efetivas do sujeito não montam a titularidade")]
    public void PontoDeDecisaoEConcessoesDoSujeito_NaoMontamTitularidade()
    {
        Types().That().ResideInNamespaceMatching(PontoDeDecisao).Or().HaveFullNameMatching(ResolvedorDoSujeito)
            .GetObjects(Arquitetura).Select(static t => t.Name).Should()
            .Contain("GrantEfetivoAplicavelCheck", "sem os tipos do ponto de decisão a conferência passaria vazia")
            .And.Contain("HttpAuthorizationSubjectResolver", "sem o assembly do resolvedor do sujeito a conferência passaria vazia");

        Falhas(Types().That().ResideInNamespaceMatching(PontoDeDecisao).Or().HaveFullNameMatching(ResolvedorDoSujeito)
                .Should().NotCallAny(FabricaDaTitularidade()))
            .Should().BeEmpty("a titularidade nunca está nas concessões efetivas do sujeito nem autoriza no ponto de decisão");
    }

    [Fact(DisplayName = "A regra encontra a fábrica da titularidade, para não passar vazia")]
    public void FabricaDaTitularidade_EncontradaPelaRegra() =>
        FabricaDaTitularidade().GetObjects(Arquitetura).Should().ContainSingle();

    private static List<string> Violacoes(string escopo) =>
    [
        .. Falhas(Types().That().ResideInNamespaceMatching(escopo)
            .And().DoNotHaveFullNameMatching($@"^Unifesspa\.UniPlus\.(Authorization\.ValueObjects\.EffectiveGrant|{Verificador})(\+|$)")
            .Should().NotCallAny(FabricaDaTitularidade())),
        .. Falhas(Types().That().ResideInNamespaceMatching(escopo)
            .And().DoNotHaveFullNameMatching($@"^Unifesspa\.UniPlus\.(Authorization\.Decisao\.AuthorizationDecisionService|{Verificador})(\+|$)")
            .Should().NotCallAny(PermitirDecisao())),
    ];

    private static IEnumerable<string> Falhas(TypesShouldConjunction regra) =>
        regra.Evaluate(Arquitetura).Where(static resultado => !resultado.Passed).Select(static resultado => resultado.Description);

    private static GivenMethodMembersConjunction FabricaDaTitularidade() =>
        MethodMembers().That().HaveNameStartingWith("PorTitularidade(").And().AreDeclaredIn(typeof(EffectiveGrant));

    private static GivenMethodMembersConjunction PermitirDecisao() =>
        MethodMembers().That().HaveNameStartingWith("Permitir(").And().AreDeclaredIn(typeof(AuthorizationDecision));

    private static ReflectionAssembly[] Assemblies() =>
        [
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static caminho => !Path.GetFileName(caminho).Contains("Tests", StringComparison.Ordinal)
                    || Path.GetFileName(caminho) == "Unifesspa.UniPlus.ArchTests.dll")
                .Select(static caminho => ReflectionAssembly.LoadFrom(caminho)),
        ];
}

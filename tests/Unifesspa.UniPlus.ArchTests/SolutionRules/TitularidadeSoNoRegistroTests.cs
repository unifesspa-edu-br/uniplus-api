namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// A titularidade só existe no registro de acesso (ADR-0166): o verificador de acesso monta a concessão efetiva de
/// titularidade e a decisão permitida que a carrega, para registrar a leitura que a parte faz do que é seu, e nada disso
/// chega ao ponto de decisão. A fonte é um valor de enum, que o compilador grava como número e a análise de dependência
/// não enxerga; por isso a regra trava quem pode montar uma concessão efetiva com fonte escolhida
/// (<see cref="EffectiveGrant.From"/>): fora o verificador, só a própria concessão efetiva, cuja fábrica da concessão
/// gravada fixa a fonte. O escopo são os assemblies de produto; os testes montam concessões à vontade.
/// </summary>
public sealed class TitularidadeSoNoRegistroTests
{
    private const string Verificador = @"Infrastructure\.Core\.Authorization\.VerificadorDeAcessoHttp";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(AssembliesDoProduto()).Build();

    [Fact(DisplayName = "Só o verificador de acesso e a própria concessão efetiva montam concessão efetiva com fonte escolhida")]
    public void ConcessaoComFonteEscolhida_SoNoVerificadorENaConcessaoEfetiva() =>
        ChamadoresFora(FabricaDaConcessaoEfetiva(), $@"^Unifesspa\.UniPlus\.(Authorization\.ValueObjects\.EffectiveGrant|{Verificador})(\+|$)")
            .Should().BeEmpty("a titularidade só nasce no verificador, para o registro; nenhum outro tipo do produto escolhe a fonte da concessão efetiva");

    [Fact(DisplayName = "Só o serviço de decisão e o verificador de acesso criam a decisão permitida")]
    public void DecisaoPermitida_SoNoServicoDeDecisaoENoVerificador() =>
        ChamadoresFora(PermitirDecisao(), $@"^Unifesspa\.UniPlus\.(Authorization\.Decisao\.AuthorizationDecisionService|{Verificador})(\+|$)")
            .Should().BeEmpty("a decisão permitida vem do ponto de decisão ou, só para o registro da titularidade, do verificador");

    [Fact(DisplayName = "O ponto de decisão e a montagem das concessões efetivas do sujeito não montam concessão com fonte escolhida")]
    public void PontoDeDecisaoEConcessoesDoSujeito_NaoMontamConcessaoComFonteEscolhida()
    {
        IEnumerable<IType> pontoDeDecisao = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Authorization\.Decisao(\.|$)")
            .Or().HaveFullNameMatching(@"^Unifesspa\.UniPlus\.Infrastructure\.Core\.Authorization\.HttpAuthorizationSubjectResolver(\+|$)")
            .GetObjects(Arquitetura).ToList();
        pontoDeDecisao.Should().Contain(static t => t.Name == "GrantEfetivoAplicavelCheck", "sem os tipos do ponto de decisão a conferência passaria vazia")
            .And.Contain(static t => t.Name == "HttpAuthorizationSubjectResolver", "sem o assembly do resolvedor do sujeito a conferência das concessões efetivas passaria vazia");

        Types().That().CallAny(FabricaDaConcessaoEfetiva()).GetObjects(Arquitetura)
            .Intersect(pontoDeDecisao)
            .Should().BeEmpty("a titularidade nunca está nas concessões efetivas do sujeito nem autoriza no ponto de decisão");
    }

    private static string[] ChamadoresFora(GivenMethodMembersConjunction metodo, string permitidos)
    {
        IType[] chamadores = [.. Types().That().CallAny(metodo).GetObjects(Arquitetura)];
        chamadores.Should().Contain(static t => Regex.IsMatch(t.FullName, Verificador), "o verificador chama a fábrica, e sem ele a conferência passaria vazia");

        return [.. chamadores.Select(static t => t.FullName).Where(n => !Regex.IsMatch(n, permitidos))];
    }

    private static GivenMethodMembersConjunction FabricaDaConcessaoEfetiva() =>
        MethodMembers().That().HaveNameStartingWith("From(").And().AreDeclaredIn(typeof(EffectiveGrant));

    private static GivenMethodMembersConjunction PermitirDecisao() =>
        MethodMembers().That().HaveNameStartingWith("Permitir(").And().AreDeclaredIn(typeof(AuthorizationDecision));

    private static ReflectionAssembly[] AssembliesDoProduto() =>
        [
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal))
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}

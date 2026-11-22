namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Filters;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class FiltroDeAutorizacaoDeclaraExigenciaTests
{
    private const string Produto = @"^Unifesspa\.UniPlus\.(?!ArchTests)";
    private const string Exemplos = @"^Unifesspa\.UniPlus\.ArchTests\.Fixtures";

    [Fact]
    public void FiltrosDeAutorizacao_DeclaramExigencia()
    {
        if (FiltrosSincronos(Produto).GetObjects(ArquiteturaDasTravas.Produto).Any())
        {
            ArquiteturaDasTravas.Reprovacoes(RegraSincrona(Produto), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        }
        ArquiteturaDasTravas.Reprovacoes(RegraAssincrona(Produto), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        ArquiteturaDasTravas.Reprovacoes(RegraSincrona(Exemplos), ArquiteturaDasTravas.Fixtures)
            .Should().Contain(static v => v.Contains(nameof(FixturesDeAutorizacao.FiltroSemMarca), StringComparison.Ordinal));
    }

    [Fact]
    public void ExigenciasDeAutorizacao_DependemDoVerificador()
    {
        ArquiteturaDasTravas.Reprovacoes(RegraDoVerificador(Produto), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        ArquiteturaDasTravas.Reprovacoes(RegraDoVerificador(Exemplos), ArquiteturaDasTravas.Fixtures)
            .Should().Contain(static v => v.Contains(nameof(FixturesDeAutorizacao.ExigenciaSemVerificador), StringComparison.Ordinal));
    }

    private static ClassesShouldConjunction RegraDoVerificador(string escopo) =>
        Classes().That().ResideInNamespaceMatching(escopo)
            .And().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin))
            .Should().DependOnAny(Types().That().Are(typeof(IVerificadorDeAcesso)));

    private static GivenClassesConjunction FiltrosSincronos(string escopo) =>
        Classes().That().ResideInNamespaceMatching(escopo)
            .And().ImplementInterface(typeof(IAuthorizationFilter));

    private static ClassesShouldConjunction RegraSincrona(string escopo) =>
        FiltrosSincronos(escopo).Should().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin));

    private static ClassesShouldConjunction RegraAssincrona(string escopo) =>
        Classes().That().ResideInNamespaceMatching(escopo).And().ImplementInterface(typeof(IAsyncAuthorizationFilter))
            .Should().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin));
}

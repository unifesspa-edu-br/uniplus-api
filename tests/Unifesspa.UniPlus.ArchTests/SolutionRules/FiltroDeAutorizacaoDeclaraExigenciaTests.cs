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
    [Fact]
    public void FiltrosDeAutorizacao_DeclaramExigencia()
    {
        if (FiltrosSincronos().GetObjects(ArquiteturaDasTravas.Produto).Any())
        {
            ArquiteturaDasTravas.Reprovacoes(RegraSincrona(), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        }
        ArquiteturaDasTravas.Reprovacoes(RegraAssincrona(), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        ArquiteturaDasTravas.Reprovacoes(RegraSincrona(), ArquiteturaDasTravas.Fixtures)
            .Should().Contain(static v => v.Contains(nameof(FixturesDeAutorizacao.FiltroSemMarca), StringComparison.Ordinal));
    }

    [Fact]
    public void ExigenciasDeAutorizacao_DependemDoVerificador()
    {
        ArquiteturaDasTravas.Reprovacoes(RegraDoVerificador(), ArquiteturaDasTravas.Produto).Should().BeEmpty();
        ArquiteturaDasTravas.Reprovacoes(RegraDoVerificador(), ArquiteturaDasTravas.Fixtures)
            .Should().Contain(static v => v.Contains(nameof(FixturesDeAutorizacao.ExigenciaSemVerificador), StringComparison.Ordinal));
    }

    private static ClassesShouldConjunction RegraDoVerificador() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .And().AreAssignableTo(typeof(Attribute)).And().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin))
            .Should().DependOnAny(Types().That().Are(typeof(IVerificadorDeAcesso)));

    private static GivenClassesConjunction FiltrosSincronos() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.").And().AreAssignableTo(typeof(Attribute))
            .And().ImplementInterface(typeof(IAuthorizationFilter));

    private static ClassesShouldConjunction RegraSincrona() =>
        FiltrosSincronos().Should().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin));

    private static ClassesShouldConjunction RegraAssincrona() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.").And().AreAssignableTo(typeof(Attribute)).And().ImplementInterface(typeof(IAsyncAuthorizationFilter))
            .Should().ImplementInterface(typeof(IExigeAutorizacaoAlemDoLogin));
}

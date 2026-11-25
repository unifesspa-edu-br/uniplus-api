namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class RegistroDaInfraestruturaTests
{
    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;

    [Fact]
    public void RegistroDaInfraestrutura_ClassesDaAplicacao_NaoDependeDelas()
    {
        string nome = typeof(SelecaoInfrastructureRegistration).FullName!;
        RegraDeDependenciaDasClassesDaAplicacao(Types().That().HaveFullNameMatching(
            $@"^{System.Text.RegularExpressions.Regex.Escape(nome)}(\+|$)"))
            .Check(Arquitetura);
    }

    [Fact]
    public void DependenciaDeRegistroGenerico_ClasseDaAplicacao_RegraReprova()
    {
        TypesShouldConjunction regra = RegraDeDependenciaDasClassesDaAplicacao(
            Types().That().HaveFullName(typeof(RegistroDeExemplo).FullName!));

        regra.Evaluate(Arquitetura).Should().Contain(static resultado => !resultado.Passed);
    }

    private static TypesShouldConjunction RegraDeDependenciaDasClassesDaAplicacao(GivenTypesConjunction tipos) =>
        tipos.Should().NotDependOnAny(Classes().That().ResideInNamespaceMatching(
            @"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)"));

    private static class RegistroDeExemplo
    {
        internal static void Adicionar(IServiceCollection servicos) => servicos.AddScoped<InscricaoDoDono>();
    }
}

namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class HandlerDeAcaoAdministrativaTests
{
    [Fact]
    public void MensagensComDecisaoNoHandler_AlcancamOVerificador()
    {
        foreach (Type mensagem in ExcecoesDeAcessoDasAcoes.DecididasNoHandler.Values)
        {
            Verificar(ArquiteturaDasTravas.Produto, mensagem).Should().BeTrue($"{mensagem.Name} decide pelo verificador de acesso");
        }
    }

    [Fact]
    public void Handler_SemVerificador_RecusadoComContraprovas()
    {
        Verificar(ArquiteturaDasTravas.Fixtures, typeof(FixturesDeAutorizacao.MensagemDireta)).Should().BeTrue();
        Verificar(ArquiteturaDasTravas.Fixtures, typeof(FixturesDeAutorizacao.MensagemPorServico)).Should().BeTrue();
        Verificar(ArquiteturaDasTravas.Fixtures, typeof(FixturesDeAutorizacao.MensagemSemVerificador)).Should().BeFalse();
    }

    private static bool Verificar(Architecture arquitetura, Type mensagem)
    {
        IEnumerable<IType> dependentes = Types().That().DependOnAny(typeof(IVerificadorDeAcesso)).GetObjects(arquitetura);
        IType[] handlers = [.. Types().That().DependOnAny(mensagem).GetObjects(arquitetura)
            .Where(static tipo => tipo.Members.OfType<MethodMember>().Any(static metodo => metodo.Name.StartsWith("Handle(", StringComparison.Ordinal)))];
        handlers.Should().ContainSingle($"{mensagem.Name} deve ter um handler");
        IType handler = handlers[0];
        return dependentes.Contains(handler)
            || Types().That().Are(handler).And().DependOnAny(dependentes).GetObjects(arquitetura).Any();
    }
}

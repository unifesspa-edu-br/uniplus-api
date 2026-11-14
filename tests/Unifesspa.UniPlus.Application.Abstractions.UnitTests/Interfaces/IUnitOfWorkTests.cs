namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Interfaces;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public sealed class IUnitOfWorkTests
{
    [Fact]
    public void IUnitOfWork_DeveExporSalvarDescartarEForcarChecagemDeConstraints()
    {
        string[] metodos = [.. typeof(IUnitOfWork).GetMethods().Select(static m => m.Name)];

        metodos.Should().BeEquivalentTo(
            "SalvarAlteracoesAsync",
            "DescartarAlteracoesNaoSalvas",
            "ForcarChecagemImediataDeConstraintsAsync");
    }

    [Fact]
    public void IUnitOfWork_SalvarAlteracoesDeveRetornarTaskDeInt()
    {
        MethodInfo metodo = typeof(IUnitOfWork).GetMethod("SalvarAlteracoesAsync")!;

        metodo.ReturnType.Should().Be<Task<int>>();
    }

    [Fact]
    public void IUnitOfWork_DescartarAlteracoesNaoDevolveNada()
    {
        MethodInfo metodo = typeof(IUnitOfWork).GetMethod("DescartarAlteracoesNaoSalvas")!;

        metodo.ReturnType.Should().Be(typeof(void));
        metodo.GetParameters().Should().BeEmpty();
    }

    [Theory]
    [InlineData("SalvarAlteracoesAsync")]
    [InlineData("ForcarChecagemImediataDeConstraintsAsync")]
    public void IUnitOfWork_MetodosAssincronosAceitamCancellationTokenComValorPadrao(string nomeDoMetodo)
    {
        MethodInfo metodo = typeof(IUnitOfWork).GetMethod(nomeDoMetodo)!;
        ParameterInfo[] parametros = metodo.GetParameters();

        parametros.Should().HaveCount(1);
        parametros[0].ParameterType.Should().Be<CancellationToken>();
        parametros[0].HasDefaultValue.Should().BeTrue();
    }
}

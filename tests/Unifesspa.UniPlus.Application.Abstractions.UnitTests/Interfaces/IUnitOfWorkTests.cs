namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Interfaces;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public sealed class IUnitOfWorkTests
{
    [Fact]
    public void IUnitOfWork_DeveTerExatamenteUmMetodo()
    {
        MethodInfo[] metodos = typeof(IUnitOfWork).GetMethods();

        metodos.Should().HaveCount(1);
    }

    [Fact]
    public void IUnitOfWork_MetodoDeveChamarSeSalvarAlteracoesAsync()
    {
        MethodInfo[] metodos = typeof(IUnitOfWork).GetMethods();

        metodos[0].Name.Should().Be("SalvarAlteracoesAsync");
    }

    [Fact]
    public void IUnitOfWork_MetodoDeveRetornarTaskDeInt()
    {
        MethodInfo metodo = typeof(IUnitOfWork).GetMethod("SalvarAlteracoesAsync")!;

        metodo.ReturnType.Should().Be<Task<int>>();
    }

    [Fact]
    public void IUnitOfWork_MetodoDeveAceitarCancellationTokenComValorPadrao()
    {
        MethodInfo metodo = typeof(IUnitOfWork).GetMethod("SalvarAlteracoesAsync")!;
        ParameterInfo[] parametros = metodo.GetParameters();

        parametros.Should().HaveCount(1);
        parametros[0].ParameterType.Should().Be<CancellationToken>();
        parametros[0].HasDefaultValue.Should().BeTrue();
    }
}

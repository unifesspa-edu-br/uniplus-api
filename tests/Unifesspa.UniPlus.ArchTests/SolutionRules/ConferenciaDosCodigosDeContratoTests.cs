namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ConferenciaDosCodigosDeContratoTests
{
    [Fact]
    public void CodigosDePermissao_NaoUsamCodigoDeContrato()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Produto;
        IType[] declarantes = [.. FieldMembers().That().HaveAnyAttributes(typeof(PermissaoAttribute))
            .GetObjects(arquitetura).Select(static campo => campo.DeclaringType).Distinct()];
        declarantes.Should().NotBeEmpty();

        FieldMembers().That().AreDeclaredIn(declarantes).Should()
            .NotHaveAnyAttributes(typeof(CodigoDeContratoAttribute)).Check(arquitetura);
    }

    [Fact]
    public void DeclarantesDeCodigosDeErro_SaoClassesSemCodigoDeContrato()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Produto;
        IType[] declarantes = [.. Types().That().HaveFullNameStartingWith("Unifesspa.UniPlus.")
            .GetObjects(arquitetura).Where(static tipo => tipo.Name.EndsWith("ErrorCodes", StringComparison.Ordinal)
                || tipo.FullName == typeof(ErrosDaFronteiraHttp).FullName)];
        declarantes.Should().NotBeEmpty();
        Classes().That().Are(declarantes).GetObjects(arquitetura).Should().HaveCount(declarantes.Length);
        FieldMembers().That().AreDeclaredIn(declarantes).Should()
            .NotHaveAnyAttributes(typeof(CodigoDeContratoAttribute)).Check(arquitetura);
    }

    [Fact]
    public void ConferenciaDosCodigos_SoEDependenteDoCatalogoDeContrato()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Produto;
        IType[] conferencia = [.. Types().That().HaveFullName(
            "Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.ConferenciaDosCodigos").GetObjects(arquitetura)];
        conferencia.Should().ContainSingle();
        IType[] dependentes = [.. Types().That().DependOnAny(conferencia).GetObjects(arquitetura)];
        dependentes.Should().NotBeEmpty();
        dependentes.Should().OnlyContain(static tipo =>
            tipo.FullName.StartsWith("Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.", StringComparison.Ordinal));
    }
}

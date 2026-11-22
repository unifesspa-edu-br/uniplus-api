namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ErrosDaFronteiraHttpRegistradosTests
{
    [Fact]
    public void Registro_AcessaTodasAsDefinicoesDaFronteira()
    {
        FieldMember[] campos = [.. FieldMembers().That().AreDeclaredIn(typeof(ErrosDaFronteiraHttp)).And().ArePublic().GetObjects(ArquiteturaDasTravas.Produto)];
        IType registro = Types().That().HaveFullName("Unifesspa.UniPlus.Infrastructure.Core.Errors.ErrosDaFronteiraHttpDomainErrorRegistration")
            .GetObjects(ArquiteturaDasTravas.Produto).Single();
        campos.Should().NotBeEmpty();
        campos.Except(registro.GetAccessedFieldMembers()).Should().BeEmpty();
    }

    [Fact]
    public void RegistroQueOmiteCampo_NaoAcessaTodosOsCampos()
    {
        FieldMember[] campos = [.. FieldMembers().That().AreDeclaredIn(typeof(DefinicoesDeExemplo)).And().ArePublic().GetObjects(ArquiteturaDasTravas.Fixtures)];
        IType registro = Types().That().Are(typeof(RegistroDeExemplo)).GetObjects(ArquiteturaDasTravas.Fixtures).Single();
        campos.Should().HaveCount(2);
        Exception? falha = Record.Exception(() => campos.Except(registro.GetAccessedFieldMembers()).Should().BeEmpty());
        falha.Should().NotBeNull();
        campos.Except(registro.GetAccessedFieldMembers()).Select(campo => campo.Name).Should().Equal(nameof(DefinicoesDeExemplo.Omitido));
    }
}

internal static class DefinicoesDeExemplo
{
    public static readonly object Registrado = new();
    public static readonly object Omitido = new();
}

internal static class RegistroDeExemplo
{
    public static object Obter() => DefinicoesDeExemplo.Registrado;
}

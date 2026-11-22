namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.Json.Serialization;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Members.FieldMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Members.PropertyMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Contratos.UnitTests.Fixtures;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ConversorJsonNaoEscondeOCodigoDeContratoTests
{
    [Fact]
    public void Produto_ConversorNaoEscondeCodigoDeContrato()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Produto;
        IType[] enums = [.. EnumsComCodigo(arquitetura).Where(static tipo => !tipo.Namespace.FullName.Contains("Tests", StringComparison.Ordinal))];
        enums.Should().NotBeEmpty();
        RegraDosTipos(enums).Check(arquitetura);
        RegraDasPropriedades(enums, @"^Unifesspa\.UniPlus\.(?!ArchTests|Contratos\.UnitTests)").Check(arquitetura);
        RegraDosCampos(enums, @"^Unifesspa\.UniPlus\.(?!ArchTests|Contratos\.UnitTests)").WithoutRequiringPositiveResults().Check(arquitetura);
    }

    [Theory]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoEnum))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoAnulavel))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeLista))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeArray))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNaChave))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNoValor))]
    [InlineData(nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumAninhado))]
    public void PropriedadeComConversor_QueMencionaEnum_Recusada(string nome)
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        ArquiteturaDasTravas.Reprovacoes(RegraDasPropriedades(EnumsComCodigo(arquitetura), @"^Unifesspa\.UniPlus\."), arquitetura)
            .Should().Contain(falha => falha.Contains(nome, StringComparison.Ordinal));
    }

    [Fact]
    public void CamposEEnumComConversor_SaoRecusados()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] enums = EnumsComCodigo(arquitetura);
        string[] falhas = [.. ArquiteturaDasTravas.Reprovacoes(RegraDosCampos(enums, @"^Unifesspa\.UniPlus\."), arquitetura)];
        falhas.Should().Contain(falha => falha.Contains(nameof(FixturesDeConversorJson.ConversorNoCampoDoEnum), StringComparison.Ordinal));
        falhas.Should().Contain(falha => falha.Contains(nameof(FixturesDeConversorJson.ConversorNoCampoDeLista), StringComparison.Ordinal));
        ArquiteturaDasTravas.Reprovacoes(RegraDosTipos(enums), arquitetura)
            .Should().Contain(falha => falha.Contains(nameof(FixturesDeConversorJson.EstadoComCodigoEConversorNoTipo), StringComparison.Ordinal));
    }

    [Fact]
    public void TiposSemConversorOuSemCodigo_SaoAceitos()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] enums = EnumsComCodigo(arquitetura);
        string[] falhas = [.. ArquiteturaDasTravas.Reprovacoes(RegraDasPropriedades(enums, @"^Unifesspa\.UniPlus\."), arquitetura)];
        falhas.Should().NotContain(falha => falha.Contains(nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo), StringComparison.Ordinal));
        falhas.Should().NotContain(falha => falha.Contains(nameof(FixturesDeConversorJson.ConversorEmTipoQueNaoMencionaEnumComCodigo), StringComparison.Ordinal));
        falhas.Should().NotContain(falha => falha.Contains(nameof(FixturesDeConversorJson.ConversorEmEnumSemCodigo), StringComparison.Ordinal));
    }

    private static IType[] EnumsComCodigo(Architecture arquitetura) =>
        [.. FieldMembers().That().AreDeclaredIn(Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\."))
            .And().HaveAnyAttributes(typeof(CodigoDeContratoAttribute))
            .GetObjects(arquitetura).Select(static campo => campo.DeclaringType).Distinct()];

    private static TypesShouldConjunction RegraDosTipos(IType[] enums) =>
        Types().That().Are(enums).Should().NotHaveAnyAttributes(typeof(JsonConverterAttribute));

    private static PropertyMembersShouldConjunction RegraDasPropriedades(IType[] enums, string escopo) =>
        PropertyMembers().That().AreDeclaredIn(Types().That().ResideInNamespaceMatching(escopo))
            .And().HaveAnyAttributes(typeof(JsonConverterAttribute)).Should().NotDependOnAny(enums);

    private static FieldMembersShouldConjunction RegraDosCampos(IType[] enums, string escopo) =>
        FieldMembers().That().AreDeclaredIn(Types().That().ResideInNamespaceMatching(escopo))
            .And().HaveAnyAttributes(typeof(JsonConverterAttribute)).Should().NotDependOnAny(enums);
}

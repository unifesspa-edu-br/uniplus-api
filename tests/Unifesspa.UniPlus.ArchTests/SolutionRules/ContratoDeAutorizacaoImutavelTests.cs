namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Members.FieldMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Members.PropertyMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ContratoDeAutorizacaoImutavelTests
{
    [Fact]
    public void RegistrosPublicos_DoContrato_SaoImutaveis()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Produto;
        IType[] registros = ContratoPublico(arquitetura, typeof(AuthorizationSubject).Assembly);
        registros.Should().NotBeEmpty();
        RegraDeImutabilidade(registros).Check(arquitetura);
        RegraDePropriedades(registros).Check(arquitetura);
        IType[] contrato = [.. Types().That().ResideInNamespace(typeof(AuthorizationSubject).Namespace!)
            .GetObjects(arquitetura).Concat(registros).Distinct()];
        foreach (IArchRule regra in RegrasDeColecoes(contrato))
        {
            regra.Check(arquitetura);
        }
        PropriedadesDeArray(contrato, arquitetura).Should().BeEmpty();
        CamposDeArray(contrato, arquitetura).Should().BeEmpty();
    }

    [Fact]
    public void RegraDeImutabilidade_RegistrosPublicosMutaveis_Recusa()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] registros = ContratoPublico(arquitetura, typeof(RegistroPublicoMutavel).Assembly);
        List<string> falhas = ArquiteturaDasTravas.Reprovacoes(RegraDeImutabilidade(registros), arquitetura);
        falhas.AddRange(ArquiteturaDasTravas.Reprovacoes(RegraDePropriedades(registros), arquitetura));
        falhas.Should().Contain(falha => falha.Contains(nameof(RegistroPublicoMutavel), StringComparison.Ordinal));
        falhas.Should().Contain(falha => falha.Contains(nameof(RegistroStructMutavel), StringComparison.Ordinal));
    }

    [Fact]
    public void RegraDeImutabilidade_RegistroComSetter_Recusa()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] registros = [.. Classes().That().Are(typeof(RegistroMutavel)).GetObjects(arquitetura)];
        ArquiteturaDasTravas.Reprovacoes(RegraDeImutabilidade(registros), arquitetura)
            .Should().Contain(falha => falha.Contains(nameof(RegistroMutavel), StringComparison.Ordinal));
    }

    [Fact]
    public void ContratoPublico_ClassesQueNaoSaoRecords_Exclui()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] tipos = ContratoPublico(arquitetura, typeof(RegistroPublicoMutavel).Assembly);
        tipos.Should().Contain(tipo => tipo.Name == nameof(RegistroPublicoMutavel));
        tipos.Should().Contain(tipo => tipo.Name == nameof(RegistroStructMutavel));
        tipos.Should().NotContain(tipo => tipo.Name == nameof(ClassePublicaComum));
        tipos.Should().NotContain(tipo => tipo.Name == nameof(AtributoPublicoAttribute));
    }

    [Theory]
    [InlineData(typeof(RegistroComLista))]
    [InlineData(typeof(RegistroComColecao))]
    [InlineData(typeof(RegistroComCampo))]
    [InlineData(typeof(RegistroComFila))]
    [InlineData(typeof(RegistroComDicionario))]
    [InlineData(typeof(RegistroComEnumeravelMutavel))]
    public void RegraDeColecoes_ColecaoMutavel_Recusa(Type tipo)
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] tipos = [.. Types().That().Are(tipo).GetObjects(arquitetura)];
        RegrasDeColecoes(tipos).SelectMany(regra => ArquiteturaDasTravas.Reprovacoes(regra, arquitetura))
            .Should().Contain(falha => falha.Contains(tipo.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void RegraDeColecoes_ListaSomenteLeituraEString_Aceita()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] tipos = [.. Types().That().Are(typeof(RegistroSomenteLeitura)).GetObjects(arquitetura)];
        RegrasDeColecoes(tipos).SelectMany(regra => ArquiteturaDasTravas.Reprovacoes(regra, arquitetura))
            .Should().BeEmpty();
        PropriedadesDeArray(tipos, arquitetura).Should().BeEmpty();
        CamposDeArray(tipos, arquitetura).Should().BeEmpty();
    }

    [Fact]
    public void RegraDeColecoes_ArrayPublico_Recusa()
    {
        Architecture arquitetura = ArquiteturaDasTravas.Fixtures;
        IType[] tipos = [.. Types().That().Are(typeof(RegistroComArray)).GetObjects(arquitetura)];
        PropriedadesDeArray(tipos, arquitetura).Should()
            .Contain(propriedade => propriedade.DeclaringType.Name == nameof(RegistroComArray));
        IType[] campos = [.. Types().That().Are(typeof(RegistroComCampoArray)).GetObjects(arquitetura)];
        CamposDeArray(campos, arquitetura).Should()
            .Contain(campo => campo.DeclaringType.Name == nameof(RegistroComCampoArray));
    }

    [Fact]
    public void DenyReason_PropriedadesPublicas_TemSomenteCodigo() =>
        RegraDoCodigo(typeof(DenyReason)).Check(ArquiteturaDasTravas.Produto);

    [Fact]
    public void RegraDoCodigo_PropriedadeAdicional_Recusa() =>
        ArquiteturaDasTravas.Reprovacoes(RegraDoCodigo(typeof(MotivoComTexto)), ArquiteturaDasTravas.Fixtures)
            .Should().Contain(falha => falha.Contains(nameof(MotivoComTexto.Texto), StringComparison.Ordinal));

    private static IType[] ContratoPublico(Architecture arquitetura, System.Reflection.Assembly assembly) =>
    [
        .. Classes().That().ResideInAssembly(assembly).And().ArePublic().And().AreNotNested().And().AreRecord()
            .And().AreNotAssignableTo(typeof(System.Attribute)).GetObjects(arquitetura),
        .. Types().That().ResideInAssembly(assembly).And().ArePublic().And().AreNotNested().And().AreStructs().GetObjects(arquitetura),
    ];

    private static ClassesShouldConjunction RegraDeImutabilidade(IType[] registros) =>
        Classes().That().Are(registros).Should().BeImmutable();

    private static PropertyMembersShouldConjunction RegraDePropriedades(IType[] registros) =>
        PropertyMembers().That().AreDeclaredIn(registros).And().HavePublicSetter().Should().HaveInitOnlySetter();

    private static IArchRule[] RegrasDeColecoes(IType[] registros) =>
    [
        RegraDeColecoesEmPropriedades(registros, typeof(System.Collections.ICollection)).WithoutRequiringPositiveResults(),
        RegraDeColecoesEmPropriedades(registros, typeof(ICollection<>)).WithoutRequiringPositiveResults(),
        RegraDeColecoesEmCampos(registros, typeof(System.Collections.ICollection)).WithoutRequiringPositiveResults(),
        RegraDeColecoesEmCampos(registros, typeof(ICollection<>)).WithoutRequiringPositiveResults(),
        RegraDePermitidosEmPropriedades(registros).WithoutRequiringPositiveResults(),
        RegraDePermitidosEmCampos(registros).WithoutRequiringPositiveResults(),
    ];

    private static GivenTypesConjunction ColecoesNaoPermitidas() =>
        Types().That().AreAssignableTo(typeof(System.Collections.IEnumerable)).And().AreNot(typeof(string))
            .And().AreNot(typeof(IReadOnlyCollection<>)).And().AreNot(typeof(IReadOnlyList<>))
            .And().AreNot(typeof(IReadOnlyDictionary<,>)).And().AreNot(typeof(IReadOnlySet<>))
            .And().AreNot(typeof(IEnumerable<>));

    private static PropertyMembersShouldConjunction RegraDePermitidosEmPropriedades(IType[] registros) =>
        PropertyMembers().That().AreDeclaredIn(registros).And().ArePublic().Should().NotDependOnAny(ColecoesNaoPermitidas());

    private static FieldMembersShouldConjunction RegraDePermitidosEmCampos(IType[] registros) =>
        FieldMembers().That().AreDeclaredIn(registros).And().ArePublic().Should().NotDependOnAny(ColecoesNaoPermitidas());

    private static PropertyMembersShouldConjunction RegraDeColecoesEmPropriedades(IType[] registros, Type colecao) =>
        PropertyMembers().That().AreDeclaredIn(registros).And().ArePublic().Should().NotDependOnAnyTypesThat()
            .AreAssignableTo(colecao);

    private static FieldMembersShouldConjunction RegraDeColecoesEmCampos(IType[] registros, Type colecao) =>
        FieldMembers().That().AreDeclaredIn(registros).And().ArePublic().Should().NotDependOnAnyTypesThat()
            .AreAssignableTo(colecao);

    private static PropertyMember[] PropriedadesDeArray(IType[] registros, Architecture arquitetura) =>
    [.. PropertyMembers().That().AreDeclaredIn(registros).And().ArePublic().GetObjects(arquitetura)
        .Where(propriedade => propriedade.IsArray)];

    private static FieldMember[] CamposDeArray(IType[] registros, Architecture arquitetura) =>
    [.. FieldMembers().That().AreDeclaredIn(registros).And().ArePublic().GetObjects(arquitetura)
        .Where(campo => campo.IsArray)];

    private static PropertyMembersShouldConjunction RegraDoCodigo(Type tipo) =>
        PropertyMembers().That().AreDeclaredIn(tipo).And().ArePublic().Should().HaveName(nameof(DenyReason.Codigo));
}

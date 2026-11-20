namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.Json.Serialization;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ArchUnitNetEnum = ArchUnitNET.Domain.Enum;
using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// O atributo <c>[JsonConverter]</c> vence as opções do serializador e esconderia o conversor de código de contrato
/// (ADR-0176): por isso nenhum membro cujo tipo declarado menciona um enum com código o leva, e nenhum enum com código
/// o leva no próprio tipo. Mencionar é ser o enum, o anulável dele, o elemento de uma coleção ou de um array, ou a chave
/// ou o valor de um dicionário, em qualquer profundidade de tipos genéricos. A representação do enum fica decidida pelas
/// opções de cada canal, e não por um segundo lugar.
/// </summary>
public sealed class ConversorJsonNaoEscondeOCodigoDeContratoTests
{
    private const string DescricaoDoMembroQueMenciona = "mencionam enum com código";

    private static readonly string[] MembrosSemConversor =
    [
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Estado),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Anulavel),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Estados),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.PorEstado),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Campo),
    ];

    private static readonly string FixturesFullName = typeof(FixturesDeConversorJson).FullName!;

    private static readonly ReflectionAssembly[] AssembliesExaminados = [typeof(FixturesDeConversorJson).Assembly, .. AssembliesDoProduto.Todos];

    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies([typeof(JsonConverterAttribute).Assembly, .. AssembliesExaminados])
        .Build();

    private static readonly HashSet<string> EnumsQueDeclaramCodigo =
    [
        .. AssembliesExaminados.SelectMany(static a => a.GetTypes()).Where(EnumsDeContrato.EhEnumQueDeclaraCodigo).Select(static t => t.FullName!),
    ];

    private static readonly IType[] TiposDoProduto =
        [.. Types().That().ResideInAssembly(AssembliesDoProduto.Todos[0], AssembliesDoProduto.Todos[1..]).GetObjects(Arquitetura)];

    private static readonly IType[] TiposDasFixtures = [.. Types().That().HaveFullNameStartingWith(FixturesFullName).GetObjects(Arquitetura)];

    private static readonly string[] MembrosDasFixturesComConversor = MembrosComConversor(TiposDasFixtures);

    private static readonly string[] EnumsDasFixturesComConversor = EnumsComConversor(TiposDasFixtures);

    public static TheoryData<string, string> MembrosQueEscondemOCodigo => new()
    {
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoEnum), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoEnum.Estado) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoAnulavel), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDoAnulavel.Estado) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeLista), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeLista.Estados) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeArray), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeArray.Estados) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNaChave), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNaChave.PorEstado) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNoValor), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumNoValor.PorNome) },
        { nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumAninhado), nameof(FixturesDeConversorJson.ConversorNaPropriedadeDeDicionarioComEnumAninhado.PorNome) },
        { nameof(FixturesDeConversorJson.ConversorNoCampoDoEnum), nameof(FixturesDeConversorJson.ConversorNoCampoDoEnum.Estado) },
        { nameof(FixturesDeConversorJson.ConversorNoCampoDeLista), nameof(FixturesDeConversorJson.ConversorNoCampoDeLista.Estados) },
    };

    [Theory(DisplayName = "O conversor JSON num membro cujo tipo menciona enum com código é reprovado, com o tipo e o membro na mensagem")]
    [MemberData(nameof(MembrosQueEscondemOCodigo))]
    public void Fixtures_ConversorNoMembroQueMencionaEnumComCodigo_Reprova(string tipo, string membro) =>
        MembrosDasFixturesComConversor.Should().Contain($"{FixturesFullName}+{tipo}.{membro}");

    [Fact(DisplayName = "O membro sem conversor JSON, que a regra examina, e o conversor num tipo que não menciona enum com código passam")]
    public void Fixtures_ConformesPassam_NosMembros()
    {
        MembrosQueMencionamEnumComCodigo(TiposDasFixtures).Should().Contain(
            [.. MembrosSemConversor.Select(static membro => $"{FixturesFullName}+{nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo)}.{membro}")],
            "a regra examina o membro correto e o aceita por não ter o atributo");

        MembrosDasFixturesComConversor.Should()
            .NotContain(static m => m.StartsWith($"{FixturesFullName}+{nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo)}.", StringComparison.Ordinal))
            .And.NotContain(static m => m.StartsWith($"{FixturesFullName}+{nameof(FixturesDeConversorJson.ConversorEmTipoQueNaoMencionaEnumComCodigo)}.", StringComparison.Ordinal))
            .And.NotContain(static m => m.StartsWith($"{FixturesFullName}+{nameof(FixturesDeConversorJson.ConversorEmEnumSemCodigo)}.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Em produção, nenhum membro cujo tipo menciona enum com código leva o conversor JSON")]
    public void Produto_MembroQueMencionaEnumComCodigo_NaoLevaConversorJson()
    {
        string[] examinados = MembrosQueMencionamEnumComCodigo(TiposDoProduto);

        examinados.Should().Contain(
        [
            $"{typeof(Perfil).FullName}.{nameof(Perfil.SituacaoDoAcesso)}",
            $"{typeof(PerfilDto).FullName}.{nameof(PerfilDto.SituacaoDoAcesso)}",
        ], "o enum com código mencionado direto e como argumento de tipo genérico são examinados; sem isso a conferência passaria vazia");

        MembrosComConversor(TiposDoProduto).Should().BeEmpty(
            "o atributo vence as opções e esconderia o conversor de código; a representação do enum é decidida pelas opções de cada canal");
    }

    [Fact(DisplayName = "O conversor JSON no próprio enum com código é reprovado, com o tipo na mensagem")]
    public void Fixtures_ConversorNoProprioEnumComCodigo_Reprova() =>
        EnumsDasFixturesComConversor.Should().ContainSingle()
            .Which.Should().Be($"{FixturesFullName}+{nameof(FixturesDeConversorJson.EstadoComCodigoEConversorNoTipo)}");

    [Fact(DisplayName = "O enum com código sem conversor e o enum sem código com conversor passam")]
    public void Fixtures_ConformesPassam_NosEnums() =>
        EnumsDasFixturesComConversor.Should()
            .NotContain($"{FixturesFullName}+{nameof(FixturesDeConversorJson.EstadoComCodigo)}")
            .And.NotContain($"{FixturesFullName}+{nameof(FixturesDeConversorJson.EstadoSemCodigoComConversorNoTipo)}");

    [Fact(DisplayName = "Em produção, nenhum enum com código leva o conversor JSON no próprio tipo")]
    public void Produto_EnumComCodigo_NaoLevaConversorJsonNoTipo()
    {
        Types().That().Are(TiposDoProduto).And().FollowCustomPredicate(DeclaraCodigo, "declaram código de contrato").GetObjects(Arquitetura)
            .Should().NotBeEmpty("sem enum com código no produto, a conferência passaria vazia");

        EnumsComConversor(TiposDoProduto).Should().BeEmpty(
            "a representação do enum com código é decidida pelas opções de cada canal, e não por um segundo lugar");
    }

    private static bool DeclaraCodigo(IType tipo) => EnumsQueDeclaramCodigo.Contains(tipo.FullName);

    private static bool MencionaEnumComCodigo(ITypeInstance<IType> instancia) =>
        DeclaraCodigo(instancia.Type) || instancia.GenericArguments.Any(MencionaEnumComCodigo);

    // Os membros que o enum declara têm o próprio enum como tipo e não são o que a regra examina.
    private static string[] MembrosQueMencionamEnumComCodigo(IType[] escopo) =>
    [
        .. PropertyMembers().That().AreDeclaredIn(escopo).And().FollowCustomPredicate(MencionaEnumComCodigo, DescricaoDoMembroQueMenciona).GetObjects(Arquitetura).Select(Descrever),
        .. FieldMembers().That().AreDeclaredIn(escopo).And().FollowCustomPredicate(MencionaEnumComCodigo, DescricaoDoMembroQueMenciona).GetObjects(Arquitetura)
            .Where(static campo => campo.DeclaringType is not ArchUnitNetEnum).Select(Descrever),
    ];

    private static string[] MembrosComConversor(IType[] escopo) =>
    [
        .. Reprovados(PropertyMembers().That().AreDeclaredIn(escopo).And().FollowCustomPredicate(MencionaEnumComCodigo, DescricaoDoMembroQueMenciona)
            .Should().NotHaveAnyAttributes(typeof(JsonConverterAttribute))),
        .. Reprovados(FieldMembers().That().AreDeclaredIn(escopo).And().FollowCustomPredicate(MencionaEnumComCodigo, DescricaoDoMembroQueMenciona)
            .Should().NotHaveAnyAttributes(typeof(JsonConverterAttribute))),
    ];

    private static string[] EnumsComConversor(IType[] escopo) =>
        Reprovados(Types().That().Are(escopo).And().FollowCustomPredicate(DeclaraCodigo, "declaram código de contrato")
            .Should().NotHaveAnyAttributes(typeof(JsonConverterAttribute)));

    private static string[] Reprovados(IArchRule regra) =>
        [.. regra.Evaluate(Arquitetura).Where(static r => !r.Passed).Select(static r => Descrever(r.EvaluatedObject))];

    // O membro vem como tipo declarante e nome; o tipo, pelo nome completo.
    private static string Descrever(object avaliado) =>
        avaliado is IMember membro ? $"{membro.DeclaringType.FullName}.{membro.Name}" : ((IType)avaliado).FullName;
}

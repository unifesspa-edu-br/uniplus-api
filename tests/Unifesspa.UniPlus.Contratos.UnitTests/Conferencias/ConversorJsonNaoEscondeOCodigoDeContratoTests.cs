namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Reflection;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Contratos.UnitTests.Fixtures;
using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;


/// <summary>
/// O atributo <c>[JsonConverter]</c> vence as opções do serializador e esconderia o conversor de código de contrato
/// (ADR-0176): por isso nenhum membro cujo tipo declarado menciona um enum com código o leva, e nenhum enum com código
/// o leva no próprio tipo. Mencionar é ser o enum, o anulável dele, o elemento de uma coleção ou de um array, ou a chave
/// ou o valor de um dicionário, em qualquer profundidade de tipos genéricos. A representação do enum fica decidida pelas
/// opções de cada canal, e não por um segundo lugar.
/// </summary>
public sealed class ConversorJsonNaoEscondeOCodigoDeContratoTests
{
    private static readonly string[] MembrosSemConversor =
    [
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Estado),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Anulavel),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Estados),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.PorEstado),
        nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo.Campo),
    ];

    private static readonly string NomeCompletoDasFixtures = typeof(FixturesDeConversorJson).NomeCompleto();


    private static readonly Type[] TiposDoProduto = [.. AssembliesDoProduto.Todos.SelectMany(static assembly => assembly.GetTypes())];
    private static readonly Type[] TiposDasFixtures = [.. typeof(FixturesDeConversorJson).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)];

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
        MembrosDasFixturesComConversor.Should().Contain($"{NomeCompletoDasFixtures}+{tipo}.{membro}");

    [Fact(DisplayName = "O membro sem conversor JSON, que a regra examina, e o conversor num tipo que não menciona enum com código passam")]
    public void Fixtures_ConformesPassam_NosMembros()
    {
        MembrosQueMencionamEnumComCodigo(TiposDasFixtures).Should().Contain(
            [.. MembrosSemConversor.Select(static membro => $"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo)}.{membro}")],
            "a regra examina o membro correto e o aceita por não ter o atributo");

        MembrosDasFixturesComConversor.Should()
            .NotContain(static m => m.StartsWith($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.SemConversorNoMembroComEnumComCodigo)}.", StringComparison.Ordinal))
            .And.NotContain(static m => m.StartsWith($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.ConversorEmTipoQueNaoMencionaEnumComCodigo)}.", StringComparison.Ordinal))
            .And.NotContain(static m => m.StartsWith($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.ConversorEmEnumSemCodigo)}.", StringComparison.Ordinal));
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
            .Which.Should().Be($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.EstadoComCodigoEConversorNoTipo)}");

    [Fact(DisplayName = "O enum com código sem conversor e o enum sem código com conversor passam")]
    public void Fixtures_ConformesPassam_NosEnums() =>
        EnumsDasFixturesComConversor.Should()
            .NotContain($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.EstadoComCodigo)}")
            .And.NotContain($"{NomeCompletoDasFixtures}+{nameof(FixturesDeConversorJson.EstadoSemCodigoComConversorNoTipo)}");

    [Fact(DisplayName = "Em produção, nenhum enum com código leva o conversor JSON no próprio tipo")]
    public void Produto_EnumComCodigo_NaoLevaConversorJsonNoTipo()
    {
        TiposDoProduto.Where(EnumsDeContrato.EhEnumQueDeclaraCodigo)
            .Should().NotBeEmpty("sem enum com código no produto, a conferência passaria vazia");

        EnumsComConversor(TiposDoProduto).Should().BeEmpty(
            "a representação do enum com código é decidida pelas opções de cada canal, e não por um segundo lugar");
    }

    private static bool MencionaEnumComCodigo(Type tipo) =>
        EnumsDeContrato.EhEnumQueDeclaraCodigo(tipo)
        || (tipo.HasElementType && MencionaEnumComCodigo(tipo.GetElementType()!))
        || tipo.GetGenericArguments().Any(MencionaEnumComCodigo);

    private static IEnumerable<(MemberInfo Membro, Type Tipo)> Membros(Type[] escopo) =>
        escopo.SelectMany(static tipo =>
            tipo.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static propriedade => (Membro: (MemberInfo)propriedade, Tipo: propriedade.PropertyType))
                .Concat(tipo.IsEnum ? [] : tipo.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(static campo => (Membro: (MemberInfo)campo, Tipo: campo.FieldType))));

    private static string[] MembrosQueMencionamEnumComCodigo(Type[] escopo) =>
        [.. Membros(escopo).Where(static membro => MencionaEnumComCodigo(membro.Tipo)).Select(static membro => Descrever(membro.Membro))];

    private static string[] MembrosComConversor(Type[] escopo) =>
        [.. Membros(escopo).Where(static membro => MencionaEnumComCodigo(membro.Tipo) && membro.Membro.IsDefined(typeof(JsonConverterAttribute), false))
            .Select(static membro => Descrever(membro.Membro))];

    private static string[] EnumsComConversor(Type[] escopo) =>
        [.. escopo.Where(static tipo => EnumsDeContrato.EhEnumQueDeclaraCodigo(tipo) && tipo.IsDefined(typeof(JsonConverterAttribute), false))
            .Select(static tipo => tipo.FullName!)];

    private static string Descrever(MemberInfo membro) => $"{membro.DeclaringType!.FullName}.{membro.Name}";
}

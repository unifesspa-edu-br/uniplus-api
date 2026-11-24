namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class EnumsDeContratoTests
{
    [Fact(DisplayName = "Os códigos declarados saem pelo tipo, sem o tipo genérico")]
    public void Declarados_PeloTipo_DevolveOsCodigos() =>
        EnumsDeContrato.Declarados(typeof(Cor)).Should().Equal(CatalogoDe<Cor>.Todos());

    [Fact(DisplayName = "A descrição do enum sai pelo tipo")]
    public void Descricao_EnumQueDeclara_DevolveATextoDeclarado() =>
        EnumsDeContrato.Descricao(typeof(Cor)).Should().Be("As cores do teste.");

    [Fact(DisplayName = "O enum que não declara descrição devolve nulo")]
    public void Descricao_EnumSemDescricao_DevolveNulo() =>
        EnumsDeContrato.Descricao(typeof(SemAtributoDeDescricao)).Should().BeNull();

    [Fact(DisplayName = "A descrição de um enum inválido lança a exceção do catálogo")]
    public void Descricao_EnumInvalido_LancaAExcecaoDoCatalogo() =>
        FluentActions.Invoking(() => EnumsDeContrato.Descricao(typeof(ComDescricaoDoEnumVazia)))
            .Should().Throw<CodigosDeContratoInvalidosException>();

    [Fact(DisplayName = "O enum inválido lança a exceção do catálogo, e não a da reflexão")]
    public void Declarados_EnumInvalido_LancaAExcecaoDoCatalogo() =>
        FluentActions.Invoking(() => EnumsDeContrato.Declarados(typeof(ComCodigoRepetido)))
            .Should().Throw<CodigosDeContratoInvalidosException>();

    [Fact(DisplayName = "Tipo que não é enum é recusado")]
    public void Declarados_TipoQueNaoEEnum_Recusa() =>
        FluentActions.Invoking(() => EnumsDeContrato.Declarados(typeof(string))).Should().Throw<ArgumentException>();

    [Fact(DisplayName = "A conferência do assembly junta os defeitos de todos os enums que declaram códigos")]
    public void ConferirEnumsDe_JuntaOsDefeitosDeTodosOsEnums()
    {
        Assembly assembly = AssemblyComEnumsDefeituosos();

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([assembly]))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos
            .Should().HaveCount(2)
            .And.Contain(d => d.Contains("Primeiro.", StringComparison.Ordinal))
            .And.Contain(d => d.Contains("Segundo.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A conferência de vários assemblies junta numa só exceção os defeitos de todos eles")]
    public void ConferirEnumsDe_VariosAssemblies_JuntaOsDefeitosDosDois()
    {
        Assembly primeiro = AssemblyComUmEnum("Alfa", ("A", "A", 1), ("A", "B", 2));
        Assembly segundo = AssemblyComUmEnum("Beta", ("X", "X", 1), ("Y", "X", 2));

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([primeiro, segundo]))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos
            .Should().HaveCount(2)
            .And.Contain(d => d.Contains("Alfa.", StringComparison.Ordinal))
            .And.Contain(d => d.Contains("Beta.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O assembly recebido mais de uma vez é conferido uma vez só")]
    public void ConferirEnumsDe_AssemblyRepetido_NaoRepeteOsDefeitos()
    {
        Assembly assembly = AssemblyComEnumsDefeituosos();

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([assembly, assembly]))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A conferência do enum com códigos válidos não lança")]
    public void ConferirEnumsDe_EnumValido_NaoLanca()
    {
        Assembly assembly = AssemblyComUmEnum("Valido", ("A", "A", 1), ("B", "B", 2));

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([assembly])).Should().NotThrow();
    }

    [Fact(DisplayName = "A conferência sem assembly algum não lança")]
    public void ConferirEnumsDe_SemAssemblies_NaoLanca() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([])).Should().NotThrow();

    [Fact(DisplayName = "A conferência recusa a lista nula")]
    public void ConferirEnumsDe_ListaNula_Recusa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe(null!)).Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "A conferência do assembly ignora o enum que não declara código algum")]
    public void ConferirEnumsDe_EnumSemCodigos_Ignora()
    {
        Assembly assembly = AssemblyComUmEnumSemCodigos();

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([assembly])).Should().NotThrow();
    }

    [Fact(DisplayName = "A conferência do executável alcança e junta os defeitos dos enums de uma referência dele")]
    public void ConferirNoExecutavel_EnumDefeituosoNumaReferencia_LancaComOsDefeitos()
    {
        Assembly executavel = EnumsCriadosEmExecucao.ExecutavelQueReferencia(typeof(Cor));

        FluentActions.Invoking(() => EnumsDeContrato.ConferirNoExecutavel(executavel))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos
            .Should().Contain(d => d.StartsWith($"{nameof(ComCodigoRepetido)}.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A conferência do executável cujas referências do Uni+ não têm defeito não lança")]
    public void ConferirNoExecutavel_ReferenciasSemDefeito_NaoLanca()
    {
        Assembly executavel = EnumsCriadosEmExecucao.ExecutavelQueReferencia(typeof(AssembliesDoUniPlus));

        FluentActions.Invoking(() => EnumsDeContrato.ConferirNoExecutavel(executavel)).Should().NotThrow();
    }

    [Fact(DisplayName = "A conferência do executável nulo é recusada")]
    public void ConferirNoExecutavel_ExecutavelNulo_Recusa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirNoExecutavel(null!)).Should().Throw<ArgumentNullException>();

    [Theory(DisplayName = "Só o enum em que algum membro declara código é enum que declara código")]
    [InlineData(typeof(Cor), true)]
    [InlineData(typeof(SemAtributoEmUmMembro), true)]
    [InlineData(typeof(SemCodigoDeContrato), false)]
    [InlineData(typeof(Cor?), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void EhEnumQueDeclaraCodigo_Tipo_DizSeODeclara(Type tipo, bool esperado) =>
        EnumsDeContrato.EhEnumQueDeclaraCodigo(tipo).Should().Be(esperado);

    [Fact(DisplayName = "A pergunta sobre o tipo recusa tipo nulo")]
    public void EhEnumQueDeclaraCodigo_TipoNulo_Recusa() =>
        FluentActions.Invoking(() => EnumsDeContrato.EhEnumQueDeclaraCodigo(null!)).Should().Throw<ArgumentNullException>();

    private static Assembly AssemblyComEnumsDefeituosos()
    {
        ModuleBuilder modulo = EnumsCriadosEmExecucao.NovoModulo();

        EnumsCriadosEmExecucao.Definir(modulo, "Primeiro", ("A", "A", 1), ("A", "B", 2));
        EnumsCriadosEmExecucao.Definir(modulo, "Segundo", ("X", "X", 1), ("Y", "X", 2));

        return modulo.Assembly;
    }

    private static Assembly AssemblyComUmEnum(string nome, params (string Codigo, string Rotulo, int Valor)[] membros)
    {
        ModuleBuilder modulo = EnumsCriadosEmExecucao.NovoModulo();
        EnumsCriadosEmExecucao.Definir(modulo, nome, membros);

        return modulo.Assembly;
    }

    private static Assembly AssemblyComUmEnumSemCodigos()
    {
        ModuleBuilder modulo = EnumsCriadosEmExecucao.NovoModulo();
        EnumBuilder semCodigos = modulo.DefineEnum("SemCodigos", TypeAttributes.Public, typeof(int));
        semCodigos.DefineLiteral("Unico", 1);
        semCodigos.CreateType();

        return modulo.Assembly;
    }
}

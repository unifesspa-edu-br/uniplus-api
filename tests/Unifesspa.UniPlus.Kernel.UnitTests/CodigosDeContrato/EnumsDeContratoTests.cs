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

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe(assembly))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos
            .Should().HaveCount(2)
            .And.Contain(d => d.Contains("Primeiro.", StringComparison.Ordinal))
            .And.Contain(d => d.Contains("Segundo.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A conferência do assembly ignora o enum que não declara código algum")]
    public void ConferirEnumsDe_EnumSemCodigos_Ignora()
    {
        Assembly assembly = AssemblyComUmEnumSemCodigos();

        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe(assembly)).Should().NotThrow();
    }

    private static Assembly AssemblyComEnumsDefeituosos()
    {
        ModuleBuilder modulo = EnumsCriadosEmExecucao.NovoModulo();

        EnumsCriadosEmExecucao.Definir(modulo, "Primeiro", ("a", "A", 1), ("a", "B", 2));
        EnumsCriadosEmExecucao.Definir(modulo, "Segundo", ("x", "X", 1), ("y", "X", 2));

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

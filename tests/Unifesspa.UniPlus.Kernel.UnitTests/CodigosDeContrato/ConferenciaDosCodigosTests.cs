namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Collections.Immutable;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ConferenciaDosCodigosTests
{
    [Fact(DisplayName = "Membro sem o atributo é recusado com o nome do membro")]
    public void MembroSemAtributo_Recusa() =>
        Defeitos<SemAtributoEmUmMembro>().Should().ContainSingle().Which.Should().Contain("SemAtributoEmUmMembro.B").And.Contain("falta");

    [Fact(DisplayName = "Código com apenas espaço acumula defeitos sem repetir o código")]
    public void CodigoApenasEspaco_AcumulaDefeitos()
    {
        IReadOnlyList<string> defeitos = Defeitos<ComCodigoApenasEspaco>();

        defeitos.Should().HaveCount(3);
        defeitos.Should().ContainSingle(d => d.Contains("código está vazio", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("espaço no começo ou no fim", StringComparison.Ordinal));
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
        defeitos.Should().AllSatisfy(d => d.Should().Contain("ComCodigoApenasEspaco.Vazio").And.NotContain("' '").And.NotContain("  "));
    }

    [Fact(DisplayName = "Código vazio é recusado apenas por estar vazio")]
    public void CodigoVazio_Recusa() =>
        Defeitos<ComCodigoVazio>().Should().ContainSingle().Which.Should().Contain("ComCodigoVazio.Vazio").And.Contain("código está vazio");

    [Fact(DisplayName = "Código repetido é recusado e cita o primeiro membro")]
    public void CodigoRepetido_Recusa() =>
        Defeitos<ComCodigoRepetido>().Should().ContainSingle().Which.Should().Contain("ComCodigoRepetido.Segundo").And.Contain("ComCodigoRepetido.Primeiro").And.NotContain("IGUAL");

    [Fact(DisplayName = "Membro com o valor numérico de outro é recusado e cita o primeiro membro")]
    public void ValorNumericoRepetido_Recusa() =>
        Defeitos<ComValorNumericoRepetido>().Should().ContainSingle().Which.Should()
            .Contain("ComValorNumericoRepetido.OutroUm").And.Contain("já é de ComValorNumericoRepetido.Um");

    [Fact(DisplayName = "Código com espaço no começo ou no fim é recusado")]
    public void CodigoComEspaco_Recusa()
    {
        IReadOnlyList<string> defeitos = Defeitos<ComEspacoNoCodigo>();
        defeitos.Should().HaveCount(2);
        defeitos.Should().ContainSingle(d => d.Contains("espaço", StringComparison.Ordinal)).Which.Should()
            .Contain("ComEspacoNoCodigo.ComEspaco").And.NotContain(" APARADO");
        defeitos.Should().ContainSingle(d => d.Contains("maiúscula", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Rótulo com espaço no começo ou no fim é recusado")]
    public void RotuloComEspaco_Recusa() =>
        Defeitos<ComEspacoNoRotulo>().Should().ContainSingle().Which.Should().Contain("ComEspacoNoRotulo.ComEspaco").And.Contain("rótulo").And.Contain("espaço");

    [Fact(DisplayName = "Código repetido com espaço nas pontas acusa espaço, formato e repetição")]
    public void CodigoRepetidoComEspaco_AcusaTodos()
    {
        IReadOnlyList<string> defeitos = Defeitos<ComEspacoERepetido>();

        defeitos.Should().HaveCount(5);
        defeitos.Count(d => d.Contains("espaço", StringComparison.Ordinal)).Should().Be(2);
        defeitos.Count(d => d.Contains("maiúscula", StringComparison.Ordinal)).Should().Be(2);
        defeitos.Should().ContainSingle(d => d.Contains("ComEspacoERepetido.Segundo", StringComparison.Ordinal) && d.Contains("já é de ComEspacoERepetido.Primeiro", StringComparison.Ordinal)).Which.Should().NotContain(" IGUAL");
    }

    [Fact(DisplayName = "Rótulo vazio é recusado")]
    public void RotuloVazio_Recusa() =>
        Defeitos<ComRotuloVazio>().Should().ContainSingle().Which.Should().Contain("ComRotuloVazio.SemRotulo").And.Contain("rótulo está vazio");

    [Fact(DisplayName = "Rótulo repetido é recusado sem diferenciar maiúsculas")]
    public void RotuloRepetido_Recusa() =>
        Defeitos<ComRotuloRepetido>().Should().ContainSingle().Which.Should().Contain("ComRotuloRepetido.B").And.Contain("ComRotuloRepetido.A");

    [Fact(DisplayName = "Descrição declarada vazia é recusada")]
    public void DescricaoVazia_Recusa() =>
        Defeitos<ComDescricaoVazia>().Should().ContainSingle().Which.Should().Contain("ComDescricaoVazia.DescricaoVazia").And.Contain("descrição");

    [Fact(DisplayName = "Descrição do enum declarada vazia é recusada")]
    public void DescricaoDoEnumVazia_Recusa() =>
        Defeitos<ComDescricaoDoEnumVazia>().Should().ContainSingle().Which.Should().Contain("ComDescricaoDoEnumVazia").And.Contain("descrição do enum");

    [Fact(DisplayName = "Todos os defeitos do enum vêm juntos, cada um com o nome do membro")]
    public void VariosDefeitos_VemJuntos()
    {
        IReadOnlyList<string> defeitos = Defeitos<ComVariosDefeitos>();

        defeitos.Should().HaveCount(3);
        defeitos.Should().Contain(d => d.Contains("ComVariosDefeitos.B", StringComparison.Ordinal) && d.Contains("código", StringComparison.Ordinal));
        defeitos.Should().Contain(d => d.Contains("ComVariosDefeitos.B", StringComparison.Ordinal) && d.Contains("rótulo", StringComparison.Ordinal));
        defeitos.Should().Contain(d => d.Contains("ComVariosDefeitos.C", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O defeito derruba toda consulta seguinte com os mesmos defeitos")]
    public void EnumInvalido_RepeteOMesmoDefeito()
    {
        IReadOnlyList<string> primeira = Defeitos<ComCodigoRepetido>();
        IReadOnlyList<string> segunda = Defeitos<ComCodigoRepetido>();

        segunda.Should().Equal(primeira);
    }

    [Fact(DisplayName = "A lista de defeitos da exceção não pode ser alterada")]
    public void DefeitosDaExcecao_NaoPodemSerAlterados()
    {
        CodigosDeContratoInvalidosException excecao = FluentActions.Invoking(CatalogoDe<ComCodigoRepetido>.Todos)
            .Should().Throw<CodigosDeContratoInvalidosException>().Which;

        IReadOnlyList<string> defeitos = excecao.Defeitos;

        defeitos.Should().NotBeAssignableTo<List<string>>();
        FluentActions.Invoking(() => ((IList<string>)defeitos).Add("outro")).Should().Throw<NotSupportedException>();
    }

    [Fact(DisplayName = "O enum sem defeito é aceito")]
    public void EnumValido_Aceita() =>
        FluentActions.Invoking(CatalogoDe<Cor>.Todos).Should().NotThrow();

    private static ImmutableArray<string> Defeitos<TEnum>()
        where TEnum : struct, Enum =>
        FluentActions.Invoking(CatalogoDe<TEnum>.Todos)
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos;
}

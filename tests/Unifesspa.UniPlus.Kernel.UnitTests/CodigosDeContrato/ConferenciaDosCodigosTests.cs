namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Collections.Immutable;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ConferenciaDosCodigosTests
{
    [Fact(DisplayName = "Membro sem o atributo é recusado com o nome do membro")]
    public void MembroSemAtributo_Recusa() =>
        Defeitos<SemAtributoEmUmMembro>().Should().ContainSingle().Which.Should().Contain("SemAtributoEmUmMembro.B").And.Contain("falta");

    [Fact(DisplayName = "Código vazio é recusado")]
    public void CodigoVazio_Recusa() =>
        Defeitos<ComCodigoVazio>().Should().ContainSingle().Which.Should().Contain("ComCodigoVazio.Vazio").And.Contain("código está vazio");

    [Fact(DisplayName = "Código repetido é recusado e cita o primeiro membro")]
    public void CodigoRepetido_Recusa() =>
        Defeitos<ComCodigoRepetido>().Should().ContainSingle().Which.Should().Contain("ComCodigoRepetido.Segundo").And.Contain("ComCodigoRepetido.Primeiro");

    [Fact(DisplayName = "Código com espaço no começo ou no fim é recusado")]
    public void CodigoComEspaco_Recusa() =>
        Defeitos<ComEspacoNoCodigo>().Should().ContainSingle().Which.Should().Contain("ComEspacoNoCodigo.ComEspaco").And.Contain("espaço");

    [Fact(DisplayName = "Rótulo com espaço no começo ou no fim é recusado")]
    public void RotuloComEspaco_Recusa() =>
        Defeitos<ComEspacoNoRotulo>().Should().ContainSingle().Which.Should().Contain("ComEspacoNoRotulo.ComEspaco").And.Contain("rótulo").And.Contain("espaço");

    [Fact(DisplayName = "Código repetido que também tem espaço nas pontas acusa o espaço e a repetição")]
    public void CodigoRepetidoComEspaco_AcusaOsDois()
    {
        IReadOnlyList<string> defeitos = Defeitos<ComEspacoERepetido>();

        defeitos.Should().HaveCount(3);
        defeitos.Count(d => d.Contains("espaço", StringComparison.Ordinal)).Should().Be(2);
        defeitos.Should().ContainSingle(d => d.Contains("ComEspacoERepetido.Segundo", StringComparison.Ordinal) && d.Contains("já é de ComEspacoERepetido.Primeiro", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Dois membros com o mesmo valor numérico são recusados")]
    public void ValorNumericoRepetido_Recusa()
    {
        Type enumeracao = EnumsCriadosEmExecucao.Definir(
            EnumsCriadosEmExecucao.NovoModulo(), "ComValorRepetido", ("um", "Um", 1), ("outro_um", "Outro um", 1));

        FluentActions.Invoking(() => EnumsDeContrato.Declarados(enumeracao))
            .Should().Throw<CodigosDeContratoInvalidosException>().Which.Defeitos
            .Should().ContainSingle().Which.Should().Contain("ComValorRepetido.Membro1").And.Contain("ComValorRepetido.Membro0");
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

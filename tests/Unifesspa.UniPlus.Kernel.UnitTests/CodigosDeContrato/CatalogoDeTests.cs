namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CatalogoDeTests
{
    [Fact(DisplayName = "O membro devolve o código, o rótulo e a descrição que declarou")]
    public void Membro_DevolveOQueDeclarou()
    {
        Cor.Azul.Codigo().Should().Be("azul");
        Cor.Azul.Rotulo().Should().Be("Azul");
        Cor.Azul.Descricao().Should().Be("A cor do céu.");
    }

    [Fact(DisplayName = "O membro sem descrição devolve descrição nula")]
    public void MembroSemDescricao_DevolveNula() =>
        Cor.VerdeClaro.Descricao().Should().BeNull();

    [Fact(DisplayName = "O código não vem do nome do membro")]
    public void Codigo_NaoVemDoNomeDoMembro() =>
        Cor.VerdeClaro.Codigo().Should().Be("verde_claro").And.NotBe(nameof(Cor.VerdeClaro));

    [Fact(DisplayName = "Os códigos declarados vêm na ordem dos membros")]
    public void Todos_NaOrdemDosMembros() =>
        CatalogoDe<Cor>.Todos().Select(static d => d.Codigo).Should().Equal("azul", "verde_claro");

    [Theory(DisplayName = "O código exato devolve o valor")]
    [InlineData("azul", Cor.Azul)]
    [InlineData("verde_claro", Cor.VerdeClaro)]
    public void TentarObter_CodigoExato_DevolveOValor(string codigo, Cor esperado)
    {
        CatalogoDe<Cor>.TentarObter(codigo, out Cor valor).Should().BeTrue();

        valor.Should().Be(esperado);
    }

    [Theory(DisplayName = "Código nulo, vazio, desconhecido, com outras maiúsculas ou com espaço não é encontrado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("roxo")]
    [InlineData("Azul")]
    [InlineData("AZUL")]
    [InlineData(" azul")]
    [InlineData("azul ")]
    public void TentarObter_CodigoQueNaoExiste_DevolveFalsoSemLancar(string? codigo) =>
        CatalogoDe<Cor>.TentarObter(codigo, out _).Should().BeFalse();

    [Fact(DisplayName = "Obter devolve o valor do código válido")]
    public void Obter_CodigoValido_DevolveOValor() =>
        CatalogoDe<Cor>.Obter("verde_claro").Should().Be(Cor.VerdeClaro);

    [Fact(DisplayName = "Obter lança quando o código não existe")]
    public void Obter_CodigoInexistente_Lanca() =>
        FluentActions.Invoking(() => CatalogoDe<Cor>.Obter("roxo"))
            .Should().Throw<ArgumentException>().Which.Message.Should().Contain("roxo").And.Contain(nameof(Cor));

    [Fact(DisplayName = "Valor fora do enum não tem código")]
    public void ValorForaDoEnum_Lanca()
    {
        Cor foraDoEnum = (Cor)99;

        FluentActions.Invoking(() => foraDoEnum.Codigo()).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => foraDoEnum.Rotulo()).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => foraDoEnum.Descricao()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Valor fora do enum de base ulong não tem código e lança a exceção documentada")]
    public void ValorForaDoEnumUlong_LancaAExcecaoDocumentada()
    {
        ComBaseUlong foraDoEnum = (ComBaseUlong)(ulong.MaxValue - 1);

        FluentActions.Invoking(() => foraDoEnum.Codigo()).Should().Throw<ArgumentOutOfRangeException>();
        ComBaseUlong.Grande.Codigo().Should().Be("grande");
    }

    [Fact(DisplayName = "Código nulo no enum inválido derruba a consulta, como qualquer outro código")]
    public void TentarObter_CodigoNuloEmEnumInvalido_Lanca() =>
        FluentActions.Invoking(() => CatalogoDe<ComCodigoRepetido>.TentarObter(null, out _))
            .Should().Throw<CodigosDeContratoInvalidosException>();

    [Fact(DisplayName = "Código qualquer no enum inválido derruba a consulta")]
    public void TentarObter_CodigoEmEnumInvalido_Lanca() =>
        FluentActions.Invoking(() => CatalogoDe<ComCodigoRepetido>.TentarObter("igual", out _))
            .Should().Throw<CodigosDeContratoInvalidosException>();

    [Fact(DisplayName = "A lista de códigos declarados não pode ser alterada por quem a recebe")]
    public void Todos_NaoPodeSerAlterada()
    {
        IReadOnlyList<CodigoDeclarado> todos = CatalogoDe<Cor>.Todos();

        todos.Should().NotBeAssignableTo<CodigoDeclarado[]>();
        FluentActions.Invoking(() => ((IList<CodigoDeclarado>)todos).Clear()).Should().Throw<NotSupportedException>();
        CatalogoDe<Cor>.Todos().Should().HaveCount(2);
    }

    [Fact(DisplayName = "Ida e volta: o código de cada membro devolve o próprio membro")]
    public void IdaEVolta_CadaMembroVoltaParaSi() =>
        Enum.GetValues<Cor>().Should().OnlyContain(static v => CatalogoDe<Cor>.Obter(v.Codigo()) == v);
}

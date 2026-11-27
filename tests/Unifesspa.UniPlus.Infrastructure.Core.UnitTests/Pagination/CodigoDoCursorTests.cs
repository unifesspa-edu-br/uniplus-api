namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O código de enumeração que viaja no ponto de continuação volta ao valor do enum só quando o contrato o declara, por
/// comparação exata; o resto é defeito do cursor, nunca um valor padrão.
/// </summary>
public sealed class CodigoDoCursorTests
{
    private enum FormatoDeTeste
    {
        [CodigoDeContrato("PRESENCIAL", "Presencial")]
        Presencial = 1,

        [CodigoDeContrato("SEMI_PRESENCIAL", "Semipresencial")]
        Semipresencial = 2,
    }

    [Fact(DisplayName = "O código declarado volta ao valor do enum")]
    public void Ler_CodigoDeclarado_VoltaAoValor()
    {
        CodigoDoCursor.Ler<FormatoDeTeste>("PRESENCIAL").Should().Be(FormatoDeTeste.Presencial);
        CodigoDoCursor.Ler<FormatoDeTeste>("SEMI_PRESENCIAL").Should().Be(FormatoDeTeste.Semipresencial);
    }

    [Theory(DisplayName = "Código em outra caixa, com espaço, vazio ou não declarado recusa o cursor")]
    [InlineData("presencial")]
    [InlineData(" PRESENCIAL")]
    [InlineData("PRESENCIAL ")]
    [InlineData("")]
    [InlineData("HIBRIDO")]
    [InlineData("1")]
    public void Ler_CodigoNaoDeclarado_RecusaOCursor(string codigo)
    {
        Action act = () => CodigoDoCursor.Ler<FormatoDeTeste>(codigo);

        act.Should().Throw<CursorAnchorMismatchException>();
    }
}

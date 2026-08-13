namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Paridade do codec de wire de <see cref="TipoRegra"/>: o <c>switch</c> de
/// <see cref="TipoRegraCodigo.ToCodigo"/> tem braço final para valores fora do
/// enum, então o compilador não acusa uma variante nova sem código canônico —
/// quem acusa é esta ida e volta sobre todos os valores não sentinela.
/// </summary>
public sealed class TipoRegraCodigoTests
{
    [Fact(DisplayName = "Todo tipo não sentinela sobrevive à ida e volta ToCodigo → FromCodigo")]
    public void ToCodigo_FromCodigo_RoundTrip_TodosOsTipos()
    {
        foreach (TipoRegra tipo in Enum.GetValues<TipoRegra>().Where(t => t != TipoRegra.Nenhuma))
        {
            string codigo = tipo.ToCodigo();

            codigo.Should().MatchRegex("^[a-z_]+$", $"o código de {tipo} é snake_case minúsculo");
            TipoRegraCodigo.FromCodigo(codigo).Should().Be(
                tipo, $"o código '{codigo}' deve materializar de volta como {tipo}");
        }
    }

    [Fact(DisplayName = "Os códigos canônicos são únicos entre os tipos")]
    public void ToCodigo_SemColisaoEntreTipos()
    {
        IEnumerable<string> codigos = Enum.GetValues<TipoRegra>()
            .Where(t => t != TipoRegra.Nenhuma)
            .Select(t => t.ToCodigo());

        codigos.Should().OnlyHaveUniqueItems("dois tipos com o mesmo código quebrariam a materialização EF");
    }

    [Fact(DisplayName = "O sentinela Nenhuma não tem código canônico")]
    public void ToCodigo_Nenhuma_Lanca()
    {
        Action acao = () => TipoRegra.Nenhuma.ToCodigo();

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Código desconhecido é rejeitado na materialização")]
    public void FromCodigo_Desconhecido_Lanca()
    {
        Action acao = () => TipoRegraCodigo.FromCodigo("tipo_inexistente");

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }
}

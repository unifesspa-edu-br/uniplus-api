namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal sealed record MetadadosDoFatoDeTeste
{
    public string? Secao { get; init; } = "DADOS";
    public int Ordem { get; init; }
    public string HipoteseLegal { get; init; } = InscricaoAdministrativaDeTeste.Hipotese;
    public string? Formato { get; init; }
    public string? Ajuda { get; init; }
    public string? Componente { get; init; }
    public string? Descricao { get; init; }
    public OrigemValoresColeta OrigemValores { get; init; } = OrigemValoresColeta.Catalogo;
    public IReadOnlyList<RestricaoValor>? Restricoes { get; init; }
}

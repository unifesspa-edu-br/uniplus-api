namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class FonteValoresFatoCodigosTests
{
    [Fact(DisplayName = "Cada fonte dos valores tem o código e o rótulo do contrato")]
    public void FonteValoresFato_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FonteValoresFato>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<FonteValoresFato, (string, string)>
            {
                [FonteValoresFato.Catalogo] = ("CATALOGO", "Catálogo"),
                [FonteValoresFato.OpcoesDoProcesso] = ("OPCOES_DO_PROCESSO", "Opções do processo"),
                [FonteValoresFato.Modalidade] = ("MODALIDADE", "Modalidades do processo"),
                [FonteValoresFato.MunicipiosDoFada] = ("MUNICIPIOS_DO_FADA", "Municípios do Fator de Atenuação das Desigualdades de Acesso (FADA)"),
                [FonteValoresFato.UnidadesFederativas] = ("UNIDADES_FEDERATIVAS", "Unidades federativas"),
                [FonteValoresFato.Municipios] = ("MUNICIPIOS", "Municípios"),
                [FonteValoresFato.TiposDeDeficiencia] = ("TIPOS_DE_DEFICIENCIA", "Tipos de deficiência"),
                [FonteValoresFato.MunicipiosProvaPresencial] = ("MUNICIPIOS_PROVA_PRESENCIAL", "Municípios da prova presencial"),
            });

    [Fact(DisplayName = "As fontes dos valores seguem a ordem de exibição e o código antigo ou desconhecido não é encontrado")]
    public void FonteValoresFato_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<FonteValoresFato>.Valores().Should().Equal(
            FonteValoresFato.Catalogo,
            FonteValoresFato.OpcoesDoProcesso,
            FonteValoresFato.Modalidade,
            FonteValoresFato.MunicipiosDoFada,
            FonteValoresFato.UnidadesFederativas,
            FonteValoresFato.Municipios,
            FonteValoresFato.TiposDeDeficiencia,
            FonteValoresFato.MunicipiosProvaPresencial);
        CatalogoDe<FonteValoresFato>.TentarObter("GLOBAL", out _).Should().BeFalse();
        CatalogoDe<FonteValoresFato>.TentarObter("GEO_UF", out _).Should().BeFalse();
        CatalogoDe<FonteValoresFato>.TentarObter("catalogo", out _).Should().BeFalse();
        CatalogoDe<FonteValoresFato>.TentarObter(null, out _).Should().BeFalse();
    }
}

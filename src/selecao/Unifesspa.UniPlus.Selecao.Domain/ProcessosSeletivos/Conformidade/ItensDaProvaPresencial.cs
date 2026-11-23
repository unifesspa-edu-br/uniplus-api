namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ItensDaProvaPresencial
{
    internal static ItemConformidade Avaliar(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        new("prova_presencial", DimensaoConformidade.ProvaPresencial,
            "Prova presencial declarada e municípios conferidos no Geo",
            ConferenciaDaProvaPresencial.Conferir(configuracao.ProvaPresencial, cadastros.MunicipiosDoGeo, cadastros.LimiteDeMunicipiosDeProva).Count == 0);
}

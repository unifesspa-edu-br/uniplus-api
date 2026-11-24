namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre a cascata de remanejamento (UNI-REQ-0056): o agregado e o detalhamento por razão.</summary>
internal static class ItensDaCascata
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao)
    {
        IReadOnlyList<FieldError> causas = CoberturaDaCascata.CausasDaCascata(configuracao);
        return
    [
        new ItemConformidade("cascata_pendente", DimensaoConformidade.CascataRemanejamento, "Cascata de remanejamento", causas.Count == 0),
        new ItemConformidade("cascata_modalidade_fora_do_regime_federal", DimensaoConformidade.CascataRemanejamento, "Cascata: modalidade SegueCascata usa a regra de distribuição federal", SemCausa(causas, CascataDeRemanejamentoErrorCodes.ForaDoRegimeFederal)),
        new ItemConformidade("cascata_origem_ausente", DimensaoConformidade.CascataRemanejamento, "Cascata: origem SegueCascata declarada na cascata de remanejamento", SemCausa(causas, CascataDeRemanejamentoErrorCodes.OrigemAusente)),
        new ItemConformidade("cascata_fallback_nao_ofertado", DimensaoConformidade.CascataRemanejamento, "Cascata: fallback e destinos resolvíveis nas modalidades ofertadas", SemCausa(causas, CascataDeRemanejamentoErrorCodes.FallbackNaoSelecionadoNaOferta)),
        new ItemConformidade("cascata_origem_nao_segue_cascata", DimensaoConformidade.CascataRemanejamento, "Cascata: origem declarada corresponde a modalidade SegueCascata ofertada", SemCausa(causas, CascataDeRemanejamentoErrorCodes.OrigemNaoSegueCascata)),
        new ItemConformidade("cascata_destino_desconhecido", DimensaoConformidade.CascataRemanejamento, "Cascata: destino declarado corresponde a modalidade ofertada", SemCausa(causas, CascataDeRemanejamentoErrorCodes.DestinoDesconhecido)),
    ];
    }

    private static bool SemCausa(IReadOnlyList<FieldError> causas, string codigo) =>
        !causas.Any(causa => string.Equals(causa.Error.Code, codigo, StringComparison.Ordinal));
}

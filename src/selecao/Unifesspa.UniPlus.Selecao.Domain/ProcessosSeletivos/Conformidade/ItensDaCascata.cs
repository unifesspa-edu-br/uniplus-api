namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre a cascata de remanejamento (UNI-REQ-0056): o agregado e o detalhamento por razão.</summary>
internal static class ItensDaCascata
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("cascata_pendente", DimensaoConformidade.CascataRemanejamento, "Cascata de remanejamento", CoberturaDaCascata.PendenciaDaCascata(estado) is null),
        new ItemConformidade("cascata_modalidade_fora_do_regime_federal", DimensaoConformidade.CascataRemanejamento, "Cascata: modalidade SegueCascata usa a regra de distribuição federal", !CoberturaDaCascata.ExisteCascataForaDoRegimeFederal(estado)),
        new ItemConformidade("cascata_origem_ausente", DimensaoConformidade.CascataRemanejamento, "Cascata: origem SegueCascata declarada na cascata de remanejamento", !CoberturaDaCascata.ExisteCascataOrigemAusente(estado)),
        new ItemConformidade("cascata_fallback_nao_ofertado", DimensaoConformidade.CascataRemanejamento, "Cascata: fallback e destinos resolvíveis nas modalidades ofertadas", !CoberturaDaCascata.ExisteCascataFallbackNaoSelecionadoNaOferta(estado)),
        new ItemConformidade("cascata_origem_nao_segue_cascata", DimensaoConformidade.CascataRemanejamento, "Cascata: origem declarada corresponde a modalidade SegueCascata ofertada", !CoberturaDaCascata.ExisteCascataOrigemNaoSegueCascata(estado)),
        new ItemConformidade("cascata_destino_desconhecido", DimensaoConformidade.CascataRemanejamento, "Cascata: destino declarado corresponde a modalidade ofertada", !CoberturaDaCascata.ExisteCascataDestinoDesconhecido(estado)),
    ];
}

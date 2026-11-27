namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// A origem das listas das exigências: os documentos exigidos, as regras de comprovação e os itens de cada grupo. A declaração guarda só
/// o que a leitura adotou, na ordem enviada, e a causa de uma conferência que aponta o item pela posição dele na lista deve achar a
/// posição em que ele veio no documento, que o item recusado antes dele desloca. O grupo adotado tem todos os itens, na posição enviada.
/// </summary>
internal static class ListasDasExigencias
{
    internal static IEnumerable<OrigemNoDocumento> Origens(DocumentosLidos documentos, IReadOnlyList<NoLido> raizes)
    {
        OrigemNoDocumento[] dosDocumentos = documentos.Adotados.Count == 0
            ? []
            : [OrigemNoDocumento.DaLista(PartesRecusadasDasExigencias.ListaDasExigencias, documentos.Adotados, NaOrdemEnviada, [])];

        return [.. dosDocumentos, .. DasRegras(PartesRecusadasDasExigencias.ListaDasRegras, raizes)];
    }

    private static IEnumerable<OrigemNoDocumento> DasRegras(string lista, IReadOnlyList<NoLido> nos)
    {
        NoLido[] adotados = [.. nos.Where(static no => no.Regra is not null)];

        return adotados.Length == 0
            ? []
            : [
                OrigemNoDocumento.DaLista(lista, [.. adotados.Select(static no => (no.Regra!, no.Posicao))], NaOrdemEnviada, []),
                .. adotados.SelectMany((no, canonica) => DasRegras($"{lista}[{canonica}].itens", no.Itens)),
            ];
    }

    private static IOrderedEnumerable<T> NaOrdemEnviada<T>(IEnumerable<T> itens) => itens.OrderBy(static _ => 0);
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// Depois que há inscrição enviada, a retificação não altera as condições em que o candidato se inscreveu
/// (UNI-REQ-0212, UNI-REQ-0144): a oferta de curso e as modalidades concorridas, oferta por oferta, não saem; o
/// valor da taxa não muda e o processo não passa de gratuito a cobrado nem de cobrado a gratuito; os critérios de
/// isenção não mudam se já há pedido de isenção. Oferta e modalidade se identificam pela origem, e nunca pelo
/// código ou pelo rótulo. O rascunho não conta. As recusas se acumulam, na ordem oferta, modalidade, taxa e
/// critérios de isenção, e a oferta e a modalidade em ordem de origem (ADR-0125). A oferta e a modalidade retiradas não têm
/// posição no documento novo, e a recusa aponta a coleção das ofertas ou a das modalidades da oferta, esta pela posição dela na
/// lista do documento novo, que chega na ordem do documento.
/// </summary>
public static class ConferenciaDasCondicoesDeInscricao
{
    private const string OfertasNoDocumento = "distribuicaoVagas";
    private const string TaxaNoDocumento = "taxaDeInscricao";

    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertasNovas,
        ConfiguracaoTaxaInscricao? taxaDaBase,
        ConfiguracaoTaxaInscricao? taxaNova,
        FatosDaExecucao fatos)
    {
        ArgumentNullException.ThrowIfNull(ofertasNovas);
        ArgumentNullException.ThrowIfNull(fatos);

        return
        [
            .. RecusasDasOfertasEDasModalidades(ofertasNovas, fatos),
            .. RecusasDaTaxa(taxaDaBase, taxaNova, fatos),
        ];
    }

    // Primeiro todas as ofertas retiradas, depois todas as modalidades retiradas, cada grupo em ordem de origem.
    private static IEnumerable<FieldError> RecusasDasOfertasEDasModalidades(
        IReadOnlyList<DistribuicaoDeVagasDaOferta> ofertasNovas, FatosDaExecucao fatos)
    {
        Dictionary<Guid, (HashSet<Guid> Modalidades, int Posicao)> novas = ofertasNovas
            .Select(static (oferta, posicao) => (oferta.OfertaCursoOrigemId, Modalidades: oferta.Modalidades.Select(static m => m.ModalidadeOrigemId).ToHashSet(), Posicao: posicao))
            .ToDictionary(static par => par.OfertaCursoOrigemId, static par => (par.Modalidades, par.Posicao));
        KeyValuePair<Guid, IReadOnlySet<Guid>>[] concorridas = [.. fatos.ModalidadesConcorridasPorOferta.OrderBy(static par => par.Key)];

        return
        [
            .. concorridas.Where(par => !novas.ContainsKey(par.Key)).Select(static _ => RecusaDaOferta()),
            .. concorridas
                .Where(par => novas.ContainsKey(par.Key))
                .SelectMany(par => RecusasDasModalidades(novas[par.Key].Posicao, par.Value, novas[par.Key].Modalidades)),
        ];
    }

    private static FieldError RecusaDaOferta() => new(
        OfertasNoDocumento,
        new DomainError(RetificacaoErrorCodes.OfertaComInscricaoNaoSai, "A oferta de curso em que há inscrição enviada não sai do documento."));

    private static IEnumerable<FieldError> RecusasDasModalidades(int posicaoDaOferta, IReadOnlySet<Guid> concorridas, HashSet<Guid> mantidas) =>
        concorridas.Where(modalidade => !mantidas.Contains(modalidade)).Order().Select(_ => new FieldError(
            $"{OfertasNoDocumento}[{posicaoDaOferta}].modalidades",
            new DomainError(RetificacaoErrorCodes.ModalidadeConcorridaNaoSai, "A modalidade em que há inscrição enviada não sai da oferta.")));

    private static IEnumerable<FieldError> RecusasDaTaxa(ConfiguracaoTaxaInscricao? taxaDaBase, ConfiguracaoTaxaInscricao? taxaNova, FatosDaExecucao fatos)
    {
        if (fatos.HaInscricaoEnviada && (taxaDaBase?.Cobra != taxaNova?.Cobra || taxaDaBase?.Valor != taxaNova?.Valor))
        {
            yield return new FieldError(
                TaxaNoDocumento,
                new DomainError(RetificacaoErrorCodes.TaxaComInscricaoNaoMuda, "Há inscrição enviada: o valor da taxa não muda, e o processo não passa de gratuito a cobrado nem de cobrado a gratuito."));
        }

        if (fatos.HaPedidoDeIsencao && !CriteriosIguais(taxaDaBase, taxaNova))
        {
            yield return new FieldError(
                $"{TaxaNoDocumento}.fundamentos",
                new DomainError(RetificacaoErrorCodes.CriteriosDeIsencaoComPedidoNaoMudam, "Há pedido de isenção feito: os critérios de isenção da taxa não mudam."));
        }
    }

    private static bool CriteriosIguais(ConfiguracaoTaxaInscricao? taxaDaBase, ConfiguracaoTaxaInscricao? taxaNova) =>
        (taxaDaBase?.Fundamentos ?? []).Order().SequenceEqual((taxaNova?.Fundamentos ?? []).Order());
}

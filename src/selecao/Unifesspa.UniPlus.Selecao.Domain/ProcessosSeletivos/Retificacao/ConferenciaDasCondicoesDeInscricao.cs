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
/// critérios de isenção, e a oferta e a modalidade em ordem de origem (ADR-0125).
/// </summary>
public static class ConferenciaDasCondicoesDeInscricao
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<DistribuicaoDeVagasDaOferta> ofertasNovas,
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
        IReadOnlyCollection<DistribuicaoDeVagasDaOferta> ofertasNovas, FatosDaExecucao fatos)
    {
        Dictionary<Guid, HashSet<Guid>> modalidadesNovas = ofertasNovas.ToDictionary(
            static o => o.OfertaCursoOrigemId, static o => o.Modalidades.Select(static m => m.ModalidadeOrigemId).ToHashSet());
        KeyValuePair<Guid, IReadOnlySet<Guid>>[] concorridas = [.. fatos.ModalidadesConcorridasPorOferta.OrderBy(static par => par.Key)];

        return
        [
            .. concorridas.Where(par => !modalidadesNovas.ContainsKey(par.Key)).Select(par => RecusaDaOferta(par.Key)),
            .. concorridas
                .Where(par => modalidadesNovas.ContainsKey(par.Key))
                .SelectMany(par => RecusasDasModalidades(par.Key, par.Value, modalidadesNovas[par.Key])),
        ];
    }

    private static FieldError RecusaDaOferta(Guid oferta) => new(
        $"ofertas[{oferta:D}]",
        new DomainError(RetificacaoErrorCodes.OfertaComInscricaoNaoSai, "A oferta de curso em que há inscrição enviada não sai do documento."));

    private static IEnumerable<FieldError> RecusasDasModalidades(Guid oferta, IReadOnlySet<Guid> concorridas, HashSet<Guid> mantidas) =>
        concorridas.Where(modalidade => !mantidas.Contains(modalidade)).Order().Select(modalidade => new FieldError(
            $"ofertas[{oferta:D}].modalidades[{modalidade:D}]",
            new DomainError(RetificacaoErrorCodes.ModalidadeConcorridaNaoSai, "A modalidade em que há inscrição enviada não sai da oferta.")));

    private static IEnumerable<FieldError> RecusasDaTaxa(ConfiguracaoTaxaInscricao? taxaDaBase, ConfiguracaoTaxaInscricao? taxaNova, FatosDaExecucao fatos)
    {
        if (fatos.HaInscricaoEnviada && (taxaDaBase?.Cobra != taxaNova?.Cobra || taxaDaBase?.Valor != taxaNova?.Valor))
        {
            yield return new FieldError(
                "taxaInscricao",
                new DomainError(RetificacaoErrorCodes.TaxaComInscricaoNaoMuda, "Há inscrição enviada: o valor da taxa não muda, e o processo não passa de gratuito a cobrado nem de cobrado a gratuito."));
        }

        if (fatos.HaPedidoDeIsencao && !CriteriosIguais(taxaDaBase, taxaNova))
        {
            yield return new FieldError(
                "taxaInscricao.fundamentos",
                new DomainError(RetificacaoErrorCodes.CriteriosDeIsencaoComPedidoNaoMudam, "Há pedido de isenção feito: os critérios de isenção da taxa não mudam."));
        }
    }

    private static bool CriteriosIguais(ConfiguracaoTaxaInscricao? taxaDaBase, ConfiguracaoTaxaInscricao? taxaNova) =>
        (taxaDaBase?.Fundamentos ?? []).Order().SequenceEqual((taxaNova?.Fundamentos ?? []).Order());
}

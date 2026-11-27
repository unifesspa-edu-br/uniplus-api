namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Domain service estático e puro: onde cada documento é coletado só significa alguma coisa
/// contra o cronograma e as etapas que estão ao lado dele.
/// </summary>
/// <remarks>
/// <para>
/// A exigência diz a fase em que o documento é coletado e, opcionalmente, a etapa daquela
/// fase — dois identificadores que não se verificam sozinhos. São três vínculos: a fase
/// pertence ao cronograma, a etapa pertence ao processo, e a etapa acontece <b>naquela</b>
/// fase. O terceiro é o que carrega sentido de negócio: apontar etapa de outra fase diria
/// que a habilitação coleta no dia da prova.
/// </para>
/// <para>
/// Vive aqui, e não dentro da escrita, porque a configuração congelada volta por um caminho
/// próprio — o envelope canônico do certame publicado — que monta o mesmo grafo sem passar
/// pela escrita. Guarda que roda num caminho só é guarda que o outro caminho não tem: uma
/// exigência apontando para fase ou etapa inexistente atravessaria a leitura e morreria na
/// chave estrangeira, como erro não tratado no meio de uma retificação; a que
/// aponta para etapa de outra fase nem isso — persiste, e o certame reidratado passa a
/// coletar o documento num dia que o cronograma não prevê.
/// </para>
/// </remarks>
public static class ValidadorVinculoDaExigencia
{
    /// <summary>
    /// Devolve o primeiro vínculo que não fecha, ou <c>null</c> quando todas as exigências
    /// apontam para fase e etapa coerentes entre si.
    /// </summary>
    public static DomainError? PrimeiroVinculoInvalido(
        IReadOnlyCollection<DocumentoExigido> exigencias,
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return exigencias.Select(item => VinculoInvalido(item, fases, etapas)).FirstOrDefault(static erro => erro is not null);
    }

    private static DomainError? VinculoInvalido(
        DocumentoExigido item, IReadOnlyCollection<FaseCronograma> fases, IReadOnlyCollection<EtapaProcesso> etapas)
    {
        FaseCronograma? faseDaExigencia = fases.FirstOrDefault(fase => fase.Id == item.ExigidoNaFaseId);
        if (faseDaExigencia is null)
        {
            return new DomainError(
                "DocumentoExigido.FaseNaoPertenceAoProcesso",
                "A fase da exigência não pertence ao cronograma deste processo.");
        }

        return item.ExigidoNaEtapaId is { } exigidoNaEtapaId ? VinculoInvalidoDaEtapa(exigidoNaEtapaId, faseDaExigencia, etapas) : null;
    }

    private static DomainError? VinculoInvalidoDaEtapa(Guid etapaId, FaseCronograma fase, IReadOnlyCollection<EtapaProcesso> etapas)
    {
        EtapaProcesso? etapa = etapas.FirstOrDefault(candidata => candidata.Id == etapaId);
        if (etapa is null)
        {
            return new DomainError("DocumentoExigido.EtapaNaoPertenceAoProcesso", "A etapa da exigência não pertence a este processo.");
        }

        return string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal)
            ? null
            : new DomainError("DocumentoExigido.EtapaNaoPertenceAFase", $"A etapa da exigência não acontece na fase {fase.Citacao}.");
    }
}

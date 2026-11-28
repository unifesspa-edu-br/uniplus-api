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
    /// Devolve as recusas dos vínculos que não fecham, na posição de cada exigência, apontando o momento dela
    /// (<c>{caminho}.momento.faseId</c> ou <c>{caminho}.momento.etapaId</c>); <paramref name="caminhosDasExigencias"/> traz,
    /// para cada exigência, o caminho do documento no pedido. Vazio quando todas as exigências
    /// apontam para fase e etapa coerentes entre si. A etapa que o processo não tem é recusada mesmo quando a
    /// fase falta; só a conferência de que a etapa acontece na fase depende das duas.
    /// </summary>
    public static IReadOnlyList<FieldError> RecusasDosVinculos(
        IReadOnlyCollection<DocumentoExigido> exigencias,
        IReadOnlyList<string> caminhosDasExigencias,
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(caminhosDasExigencias);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. exigencias.SelectMany((item, indice) => RecusasDaExigencia(item, caminhosDasExigencias[indice], fases, etapas))];
    }

    private static IEnumerable<FieldError> RecusasDaExigencia(
        DocumentoExigido item, string caminho, IReadOnlyCollection<FaseCronograma> fases, IReadOnlyCollection<EtapaProcesso> etapas)
    {
        FaseCronograma? fase = fases.FirstOrDefault(candidata => candidata.Id == item.ExigidoNaFaseId);
        if (fase is null)
        {
            yield return new FieldError($"{caminho}.momento.faseId", new DomainError(
                "DocumentoExigido.FaseNaoPertenceAoProcesso",
                "A fase da exigência deve ser uma das fases do cronograma do processo."));
        }

        if (item.ExigidoNaEtapaId is { } etapaId && RecusaDaEtapa(etapaId, caminho, fase, etapas) is { } recusa)
        {
            yield return recusa;
        }
    }

    private static FieldError? RecusaDaEtapa(Guid etapaId, string caminho, FaseCronograma? fase, IReadOnlyCollection<EtapaProcesso> etapas)
    {
        string campo = $"{caminho}.momento.etapaId";
        EtapaProcesso? etapa = etapas.FirstOrDefault(candidata => candidata.Id == etapaId);
        if (etapa is null)
        {
            return new FieldError(campo, new DomainError(
                "DocumentoExigido.EtapaNaoPertenceAoProcesso",
                "A etapa da exigência deve ser uma das etapas do processo."));
        }

        return fase is null || string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal)
            ? null
            : new FieldError(campo, new DomainError(
                "DocumentoExigido.EtapaNaoPertenceAFase",
                $"A etapa '{etapa.Nome}' não acontece na fase {fase.Citacao}."));
    }
}

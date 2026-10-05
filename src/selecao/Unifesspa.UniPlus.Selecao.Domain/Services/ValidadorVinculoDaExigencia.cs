namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

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
/// A finalidade diz o formulário a que o documento pertence, e só vale numa fase que responde o
/// formulário dela. Na fase que responde algum formulário, ela é obrigatória: inscrição e isenção
/// podem dividir a fase, e sem a finalidade não se sabe em que formulário o documento aparece. A
/// regra olha as flags da fase, e não a existência do formulário, para que exigências e formulários
/// possam ser configurados em qualquer ordem.
/// </para>
/// <para>
/// Vive aqui, e não dentro da escrita, porque a configuração congelada volta por um caminho
/// próprio — o envelope canônico do certame publicado — que monta o mesmo grafo sem passar
/// pela escrita. Guarda que roda num caminho só é guarda que o outro caminho não tem: uma
/// exigência apontando para fase ou etapa inexistente atravessaria a leitura e morreria na
/// chave estrangeira, como erro não tratado no meio de um descarte de retificação; a que
/// aponta para etapa de outra fase nem isso — persiste, e o certame restaurado passa a
/// coletar o documento num dia que o cronograma não prevê.
/// </para>
/// </remarks>
public static class ValidadorVinculoDaExigencia
{
    /// <summary>
    /// Devolve o primeiro vínculo que não fecha, ou <c>null</c> quando todas as exigências
    /// apontam para fase, formulário e etapa coerentes entre si.
    /// </summary>
    public static DomainError? PrimeiroVinculoInvalido(
        IReadOnlyCollection<DocumentoExigido> exigencias,
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        foreach (DocumentoExigido item in exigencias)
        {
            FaseCronograma? faseDaExigencia = fases.FirstOrDefault(fase => fase.Id == item.ExigidoNaFaseId);
            if (faseDaExigencia is null)
            {
                return new DomainError(
                    "DocumentoExigido.FaseNaoPertenceAoProcesso",
                    $"A fase {item.ExigidoNaFaseId} não pertence ao cronograma deste processo.");
            }

            if (RecusaDaFinalidade(item.Finalidade, faseDaExigencia) is { } recusaDaFinalidade)
            {
                return recusaDaFinalidade;
            }

            if (item.ExigidoNaEtapaId is not { } exigidoNaEtapaId)
            {
                continue;
            }

            EtapaProcesso? etapaDaExigencia = etapas.FirstOrDefault(etapa => etapa.Id == exigidoNaEtapaId);
            if (etapaDaExigencia is null)
            {
                return new DomainError(
                    "DocumentoExigido.EtapaNaoPertenceAoProcesso",
                    $"A etapa {exigidoNaEtapaId} não pertence a este processo.");
            }

            if (!string.Equals(etapaDaExigencia.FaseCodigo, faseDaExigencia.Codigo, StringComparison.Ordinal))
            {
                return new DomainError(
                    "DocumentoExigido.EtapaNaoPertenceAFase",
                    $"A etapa {exigidoNaEtapaId} não acontece na fase {faseDaExigencia.Codigo}.");
            }
        }

        return null;
    }

    /// <summary>
    /// A finalidade declarada para um documento exigido na fase: obrigatória quando a fase responde
    /// algum formulário, e então uma das que a fase responde; nula quando a fase não responde nenhum.
    /// </summary>
    public static DomainError? RecusaDaFinalidade(FinalidadeFormulario? finalidade, FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);

        IReadOnlyList<FinalidadeFormulario> atendidas = fase.FinalidadesQueAtende();
        if (finalidade is not { } declarada)
        {
            return atendidas.Count == 0
                ? null
                : new DomainError(
                    DocumentoExigidoErrorCodes.FinalidadeObrigatoria,
                    $"A fase '{fase.Codigo}' responde o formulário de {string.Join(" e de ", atendidas.Select(EstruturaFormulario.ParaToken))}; informe a que formulário o documento pertence.");
        }

        return atendidas.Contains(declarada)
            ? null
            : new DomainError(
                DocumentoExigidoErrorCodes.FaseIncoerenteComFinalidade,
                $"A fase '{fase.Codigo}' não é a fase em que se responde o formulário de {EstruturaFormulario.ParaToken(declarada)}.");
    }
}

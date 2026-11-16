namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O que a retificação faz com os motivos de decisão (UNI-REQ-0124, UNI-REQ-0285): o motivo já publicado não sai;
/// o motivo novo entra com justificativa e só enquanto a avaliação da decisão do tipo dele não começou, medida
/// sobre o cronograma da versão publicada vigente e pelo primeiro registro de decisão do tipo. O motivo se
/// identifica pela origem no cadastro. As recusas se acumulam, uma por motivo e no campo dele: as retiradas e depois
/// as inclusões, cada grupo em ordem de código; na inclusão vale a justificativa antes do início da avaliação.
/// </summary>
public static class ConferenciaDosMotivosDaRetificacao
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<MotivoDoDocumento> motivosDaBase,
        IReadOnlyCollection<MotivoDoDocumento> motivosNovos,
        AndamentoDasFases andamento)
    {
        ArgumentNullException.ThrowIfNull(motivosDaBase);
        ArgumentNullException.ThrowIfNull(motivosNovos);
        ArgumentNullException.ThrowIfNull(andamento);

        HashSet<Guid> daBase = [.. motivosDaBase.Select(static m => m.OrigemId)];
        HashSet<Guid> novos = [.. motivosNovos.Select(static m => m.OrigemId)];

        return
        [
            .. motivosDaBase.Where(m => !novos.Contains(m.OrigemId)).OrderBy(static m => m.Codigo, StringComparer.Ordinal).Select(Retirada),
            .. motivosNovos.Where(m => !daBase.Contains(m.OrigemId)).OrderBy(static m => m.Codigo, StringComparer.Ordinal)
                .Select(m => Inclusao(m, andamento)).OfType<FieldError>(),
        ];
    }

    private static FieldError Retirada(MotivoDoDocumento motivo) => new(
        Campo(motivo),
        new DomainError(
            MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao,
            $"O motivo {motivo.Codigo} já consta do edital publicado e não pode ser retirado; a retificação só inclui motivos."));

    private static FieldError? Inclusao(MotivoDoDocumento motivo, AndamentoDasFases andamento) =>
        (EscolhaDosMotivos.RecusaDaJustificativa(motivo.Justificativa) ?? RecusaDaAvaliacaoIniciada(motivo.Tipo, andamento)) is { } recusa
            ? new FieldError(Campo(motivo), recusa)
            : null;

    private static DomainError? RecusaDaAvaliacaoIniciada(TipoMotivoDeDecisao tipo, AndamentoDasFases andamento)
    {
        DateTimeOffset? primeiroRegistro = andamento.Fatos.PrimeiroRegistroDeDecisaoPorTipo.TryGetValue(tipo, out DateTimeOffset registro) ? registro : null;

        return InicioDaAvaliacao.JaComecou(andamento.FasesDaBase, andamento.EtapasDaBase, tipo, primeiroRegistro, andamento.Fatos.Instante)
            ? new DomainError(
                MotivoDeDecisaoErrorCodes.AvaliacaoIniciada,
                $"A avaliação de {tipo.ToCodigo()} já começou neste processo; motivo novo desse tipo não entra mais.")
            : null;
    }

    private static string Campo(MotivoDoDocumento motivo) => $"motivosDeDecisao[{motivo.Codigo}]";
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O que a retificação faz com os motivos de decisão (UNI-REQ-0124, UNI-REQ-0285): o motivo já publicado não sai;
/// o motivo novo entra com justificativa e só enquanto a avaliação no lugar dele não começou, medida sobre a versão
/// publicada vigente. O motivo se identifica pelo lugar e pela origem no cadastro. As recusas se acumulam, uma por
/// motivo e no campo dele: as retiradas e depois as inclusões, cada grupo em ordem de código; na inclusão vale a
/// justificativa antes do início da avaliação.
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

        HashSet<(LugarDaDecisao, Guid)> daBase = [.. motivosDaBase.Select(Identidade)];
        HashSet<(LugarDaDecisao, Guid)> novos = [.. motivosNovos.Select(Identidade)];

        return
        [
            .. motivosDaBase.Where(m => !novos.Contains(Identidade(m))).OrderBy(static m => m.Codigo, StringComparer.Ordinal).Select(Retirada),
            .. motivosNovos.Where(m => !daBase.Contains(Identidade(m))).OrderBy(static m => m.Codigo, StringComparer.Ordinal)
                .Select(m => Inclusao(m, andamento)).OfType<FieldError>(),
        ];
    }

    private static (LugarDaDecisao Lugar, Guid OrigemId) Identidade(MotivoDoDocumento motivo) => (motivo.Lugar, motivo.OrigemId);

    private static FieldError Retirada(MotivoDoDocumento motivo) => new(
        Campo(motivo),
        new DomainError(
            MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao,
            $"O motivo {motivo.Codigo} já consta do edital publicado e não pode ser retirado; a retificação só inclui motivos."));

    private static FieldError? Inclusao(MotivoDoDocumento motivo, AndamentoDasFases andamento) =>
        (EscolhaDosMotivos.RecusaDaJustificativa(motivo.Justificativa) ?? RecusaDaAvaliacaoIniciada(motivo, andamento)) is { } recusa
            ? new FieldError(Campo(motivo), recusa)
            : null;

    private static DomainError? RecusaDaAvaliacaoIniciada(MotivoDoDocumento motivo, AndamentoDasFases andamento) =>
        andamento.Fatos.FasesIniciadas.AvaliacaoComecou(motivo.Lugar)
            ? new DomainError(
                MotivoDeDecisaoErrorCodes.AvaliacaoIniciada,
                $"A avaliação de {motivo.Tipo.ToCodigo()} nesta fase ou etapa já começou; motivo novo não entra mais.")
            : null;

    private static string Campo(MotivoDoDocumento motivo) => $"motivosDeDecisao[{motivo.Codigo}]";
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Remove fisicamente o rascunho do dono do token. O rascunho não tem número nem comprovante, e guardar
/// dado pessoal sem finalidade contraria a LGPD, e por isso os arquivos de documento saem também do
/// armazenamento; a inscrição enviada nunca passa por aqui.
/// </summary>
public static class DescartarRascunhoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRascunhoDaInscricaoCommand command,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        PeriodoDaInscricao periodo,
        DescarteDoRascunho descarte,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(descarte);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(command.ProcessoSeletivoId, userContext, "descartar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result.Failure(inscricao.Error!)
            : await DescartarNoPeriodoAsync(command, inscricao.Value!, periodo, descarte, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DescartarNoPeriodoAsync(
        DescartarRascunhoDaInscricaoCommand command,
        Inscricao inscricao,
        PeriodoDaInscricao periodo,
        DescarteDoRascunho descarte,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        // Com as inscrições encerradas o rascunho não tem ação (AcoesPermitidas): a mesma regra da lista.
        DomainError? foraDoPeriodo = await periodo.ConferirAsync(command.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false);
        return foraDoPeriodo is not null
            ? Result.Failure(foraDoPeriodo)
            : await descarte.DescartarAsync(inscricao, command.Precondicao, cancellationToken).ConfigureAwait(false);
    }
}

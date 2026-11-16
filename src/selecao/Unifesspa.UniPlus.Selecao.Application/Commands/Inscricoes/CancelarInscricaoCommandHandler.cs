namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Cancela a inscrição enviada do dono do token. Só com as inscrições abertas: a janela vem da versão
/// divulgada, a mesma do envio. Fora do período a recusa é sempre a do período, porque é o estado final: a
/// orientação de descartar ou de iniciar outra só vale com as inscrições abertas.
/// </summary>
public static class CancelarInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        CancelarInscricaoCommand command,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        PeriodoDaInscricao periodo,
        GravacaoDaInscricao gravacao,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(command.ProcessoSeletivoId, userContext, "cancelar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result.Failure(inscricao.Error!)
            : await CancelarAsync(command, inscricao.Value!, periodo, gravacao, userContext, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> CancelarAsync(
        CancelarInscricaoCommand command,
        Inscricao inscricao,
        PeriodoDaInscricao periodo,
        GravacaoDaInscricao gravacao,
        IUserContext userContext,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        DomainError? recusa = await periodo.ConferirAsync(command.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false)
            ?? inscricao.ConferirCancelamento();
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        Result cancelada = inscricao.Cancelar(command.Motivo, userContext.UserId!, instante, command.Precondicao);
        return cancelada.IsFailure
            ? cancelada
            : await gravacao.SalvarAsync(inscricao, comprovante: null, cancellationToken).ConfigureAwait(false);
    }
}

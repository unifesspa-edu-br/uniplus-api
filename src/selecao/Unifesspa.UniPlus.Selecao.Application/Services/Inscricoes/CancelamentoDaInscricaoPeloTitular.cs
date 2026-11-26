namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Commands.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>Confere o período divulgado e prepara o cancelamento solicitado pelo titular.</summary>
public sealed class CancelamentoDaInscricaoPeloTitular(
    PeriodoDaInscricaoResolver periodo, IInscricaoRepository inscricoes, IUserContext usuario, TimeProvider timeProvider)
{
    public async Task<Result> CancelarAsync(Inscricao inscricao, CancelarInscricaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(command);
        DateTimeOffset instante = timeProvider.GetUtcNow();
        DomainError? foraDoPeriodo = (await periodo.ConferirAsync(command.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false))?.Recusa;
        if (foraDoPeriodo is not null)
        {
            return Result.Failure(foraDoPeriodo);
        }
        return inscricao.ConferirCancelamento() is { } recusa
            ? Result.Failure(recusa)
            : await RegistrarCancelamentoAsync(inscricao, command, instante, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> RegistrarCancelamentoAsync(
        Inscricao inscricao, CancelarInscricaoCommand command, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result cancelada = inscricao.Cancelar(command.Motivo, usuario.UserId!, instante, command.Precondicao);
        if (cancelada.IsSuccess)
        {
            await inscricoes.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        }
        return cancelada;
    }
}

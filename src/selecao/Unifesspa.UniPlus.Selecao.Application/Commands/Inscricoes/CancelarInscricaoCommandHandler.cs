namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using MotivosDecisaoIsencao;

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
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode cancelar a inscrição."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        if (cpf.IsFailure)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(command.ProcessoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result.Failure(erroDono);
        }

        DateTimeOffset instante = timeProvider.GetUtcNow();
        DomainError? foraDoPeriodo = await Services.PeriodoDaInscricaoResolver.ConferirAsync(
            command.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            instante, cancellationToken).ConfigureAwait(false);
        if (foraDoPeriodo is not null)
        {
            return Result.Failure(foraDoPeriodo);
        }

        if (inscricao.ConferirCancelamento() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Result cancelada = inscricao.Cancelar(command.Motivo, userContext.UserId!, instante, command.Precondicao);
        if (cancelada.IsFailure)
        {
            return cancelada;
        }

        await inscricaoRepository.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // O endpoint tem Idempotency-Key (ADR-0119): captura local e descarte, para o
            // SaveChangesAsync do outbox não reencontrar a entidade modificada.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}

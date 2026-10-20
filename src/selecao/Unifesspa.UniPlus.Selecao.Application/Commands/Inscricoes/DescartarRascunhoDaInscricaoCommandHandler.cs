namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Remove fisicamente o rascunho do dono do token. O rascunho não tem número nem comprovante, e guardar
/// dado pessoal sem finalidade contraria a LGPD; a inscrição enviada nunca passa por aqui.
/// </summary>
public static class DescartarRascunhoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRascunhoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode descartar a inscrição."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        if (cpf.IsFailure)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result.Failure(erroDono);
        }

        // O If-Match que o candidato tem é o do rascunho já levado à versão divulgada, que é o que a
        // leitura devolve; conferir contra o gravado recusaria para sempre o ETag que ele recebeu.
        Result adotada = await Services.VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result.Failure(adotada.Error!);
        }

        if (inscricao.ConferirDescarte(command.Precondicao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        await inscricaoRepository.RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

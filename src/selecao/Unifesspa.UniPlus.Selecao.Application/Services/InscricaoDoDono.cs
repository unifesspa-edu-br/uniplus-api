namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A inscrição de que o token é dono, pelo id: a de outro CPF responde como inexistente, porque o id não confirma a existência da
/// inscrição de ninguém, e o mesmo CPF com outro <c>sub</c> é identidade trocada (<see cref="Inscricao.ConferirDono"/>).
/// </summary>
internal static class InscricaoDoDono
{
    public static async Task<Result<Inscricao>> ObterAsync(
        Guid inscricaoId, IInscricaoRepository inscricaoRepository, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<Cpf> cpf = CpfDoToken(userContext);
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(cpf.Error!);
        }

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        if (inscricao is null || inscricao.Cpf != cpf.Value!)
        {
            return Result<Inscricao>.Failure(Inscricao.NaoEncontrada);
        }

        return inscricao.ConferirDono(userContext.UserId) is { } erroDono ? Result<Inscricao>.Failure(erroDono) : Result<Inscricao>.Success(inscricao);
    }

    /// <summary>O CPF do token, ou a recusa de quem chega sem o claim ou com um que não é CPF.</summary>
    public static Result<Cpf> CpfDoToken(IUserContext userContext)
    {
        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<Cpf>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O token não traz o claim cpf — só um candidato autenticado com CPF pode acessar as próprias inscrições."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);

        return cpf.IsFailure
            ? Result<Cpf>.Failure(new DomainError("Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."))
            : cpf;
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A inscrição de que o token é dono, pelo id: a de outro CPF responde como inexistente, porque o id não confirma a existência da
/// inscrição de ninguém, e o mesmo CPF com outro <c>contaId</c> é identidade trocada (<see cref="Inscricao.ConferirDono"/>).
/// </summary>
internal static class InscricaoDoDono
{
    public static async Task<Result<Inscricao>> ObterAsync(
        Guid inscricaoId, IInscricaoRepository inscricaoRepository, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<Cpf> cpf = CpfDoToken(userContext, "acessar as próprias inscrições");
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(cpf.Error!);
        }

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        return DoDono(inscricao is not null && inscricao.Cpf == cpf.Value! ? inscricao : null, userContext);
    }

    /// <summary>A inscrição como do dono do token: ausente responde como inexistente e a de outra conta é identidade trocada.</summary>
    public static Result<Inscricao> DoDono(Inscricao? inscricao, IUserContext userContext) =>
        inscricao is null
            ? Result<Inscricao>.Failure(Inscricao.NaoEncontrada)
            : inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono
                ? Result<Inscricao>.Failure(erroDono)
                : Result<Inscricao>.Success(inscricao);

    /// <summary>
    /// O CPF do token, ou a recusa de quem chega sem o claim ou com um que não é CPF; <paramref name="acao"/> diz o que o
    /// candidato tentava fazer, para a mensagem orientar.
    /// </summary>
    public static Result<Cpf> CpfDoToken(IUserContext userContext, string acao)
    {
        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<Cpf>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", $"O token não traz o claim cpf — só um candidato autenticado com CPF pode {acao}."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);

        return cpf.IsFailure
            ? Result<Cpf>.Failure(new DomainError("Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."))
            : cpf;
    }
}

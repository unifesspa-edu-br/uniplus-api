namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A inscrição ativa do candidato no processo seletivo, a de que o token é dono: o CPF do token, a inscrição ativa desse CPF
/// no processo e a conferência do dono. A de outro CPF responde como inexistente e a mesma pessoa com outra conta é
/// identidade trocada (<see cref="InscricaoDoDono"/>).
/// </summary>
public sealed class InscricaoAtivaDoCandidato(IInscricaoRepository inscricoes)
{
    /// <param name="processoSeletivoId">O processo seletivo da inscrição.</param>
    /// <param name="userContext">O token do candidato.</param>
    /// <param name="acao">O que o candidato tenta fazer, para a recusa de quem chega sem CPF orientar.</param>
    /// <param name="cancellationToken">O token de cancelamento.</param>
    public async Task<Result<Inscricao>> ObterAsync(
        Guid processoSeletivoId, IUserContext userContext, string acao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        Result<Cpf> cpf = InscricaoDoDono.CpfDoToken(userContext, acao);
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(cpf.Error!);
        }

        Inscricao? inscricao = await inscricoes.ObterAtivaPorProcessoECpfAsync(processoSeletivoId, cpf.Value!, cancellationToken).ConfigureAwait(false);
        return InscricaoDoDono.DoDono(inscricao, userContext);
    }
}

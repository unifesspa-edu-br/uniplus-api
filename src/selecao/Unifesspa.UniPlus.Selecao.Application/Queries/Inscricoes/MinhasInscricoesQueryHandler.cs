namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lista as inscrições do dono do token — nunca de outro CPF, mesmo que o <c>sub</c> coincida por
/// engano de emissor (<see cref="Inscricao.ConferirDono"/> confere o <c>sub</c> gravado em cada
/// uma).
/// </summary>
public static class MinhasInscricoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<MinhaInscricaoDto>>> Handle(
        MinhasInscricoesQuery query,
        IInscricaoRepository inscricaoRepository,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode listar as próprias inscrições."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<IReadOnlyList<MinhaInscricaoDto>>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        IReadOnlyList<Inscricao> inscricoes = await inscricaoRepository
            .ObterTodasPorCpfAsync(cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);

        // O mesmo CPF cifrado pode pertencer a um sub diferente do gravado (engano de emissor,
        // ver Inscricao.ConferirDono) — uma inscrição assim não é do dono do token, e listá-la
        // vazaria estado de outro candidato.
        string sub = userContext.UserId ?? string.Empty;
        return Result<IReadOnlyList<MinhaInscricaoDto>>.Success(
            [.. inscricoes.Where(i => i.ConferirDono(sub) is null).Select(MinhaInscricaoDto.De)]);
    }
}

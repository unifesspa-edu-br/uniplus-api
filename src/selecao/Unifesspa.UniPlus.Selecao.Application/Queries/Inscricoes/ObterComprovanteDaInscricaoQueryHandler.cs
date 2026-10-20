namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Devolve o comprovante de uma inscrição pelo id, ativa ou cancelada. A inscrição de outro CPF responde
/// como inexistente: o id não confirma a existência da inscrição de ninguém.
/// </summary>
public static class ObterComprovanteDaInscricaoQueryHandler
{
    public static async Task<Result<ComprovanteInscricaoDto>> Handle(
        ObterComprovanteDaInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode ler o comprovante."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorIdAsync(query.InscricaoId, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null || inscricao.Cpf != cpfResult.Value!)
        {
            return Result<ComprovanteInscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<ComprovanteInscricaoDto>.Failure(erroDono);
        }

        return await ObterComprovanteQueryHandler.LerAsync(inscricao, query.Numero, inscricaoRepository, cancellationToken)
            .ConfigureAwait(false);
    }
}

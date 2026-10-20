namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Devolve o comprovante número <see cref="ObterComprovanteQuery.Numero"/> da inscrição do dono do
/// token — nunca de outro CPF (<see cref="Inscricao.ConferirDono"/>).
/// </summary>
public static class ObterComprovanteQueryHandler
{
    public static async Task<Result<ComprovanteInscricaoDto>> Handle(
        ObterComprovanteQuery query,
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
            .ObterAtivaPorProcessoECpfAsync(query.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<ComprovanteInscricaoDto>.Failure(erroDono);
        }

        return await LerAsync(inscricao, query.Numero, inscricaoRepository, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>O comprovante número <paramref name="numero"/> da inscrição, já conferido o dono: o que as duas rotas de leitura têm em comum.</summary>
    internal static async Task<Result<ComprovanteInscricaoDto>> LerAsync(
        Inscricao inscricao, int numero, IInscricaoRepository inscricaoRepository, CancellationToken cancellationToken)
    {
        // O comprovante 1 é a primeira submissão; cada correção emite o próximo (append-only) — a
        // ordem de emissão é a ordem cronológica, nunca a de armazenamento.
        IReadOnlyList<ComprovanteInscricao> comprovantes = await inscricaoRepository
            .ObterComprovantesAsync([inscricao.Id], cancellationToken)
            .ConfigureAwait(false);
        ComprovanteInscricao? comprovante = numero > 0 ? comprovantes.ElementAtOrDefault(numero - 1) : null;
        if (comprovante is null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ComprovanteNaoEncontrado,
                $"A inscrição não tem o comprovante número {numero}."));
        }

        return Result<ComprovanteInscricaoDto>.Success(ComprovanteInscricaoDto.De(comprovante, inscricao.Cpf));
    }
}

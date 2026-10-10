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
            .ObterPorProcessoECpfAsync(query.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.NaoEncontrada,
                $"Nenhuma inscrição de {cpfDoToken} no processo seletivo {query.ProcessoSeletivoId}."));
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<ComprovanteInscricaoDto>.Failure(erroDono);
        }

        // O comprovante 1 é a primeira submissão; cada correção emite o próximo (append-only,
        // CA-01) — a ordem de emissão é a ordem cronológica, nunca a de armazenamento.
        ComprovanteInscricao? comprovante = inscricao.Comprovantes
            .OrderBy(static c => c.Instante)
            .ElementAtOrDefault(query.Numero - 1);
        if (comprovante is null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ComprovanteNaoEncontrado,
                $"A inscrição não tem o comprovante número {query.Numero}."));
        }

        return Result<ComprovanteInscricaoDto>.Success(ComprovanteInscricaoDto.De(comprovante));
    }
}

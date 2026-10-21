namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Devolve a inscrição do dono do token — nunca de outro CPF, mesmo que o <c>sub</c> coincida por
/// engano de emissor (<see cref="Inscricao.ConferirDono"/> confere o <c>sub</c> gravado) —, com a
/// avaliação do que ela gravou.
/// </summary>
public static class ObterInscricaoQueryHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        ObterInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode ler a inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(query.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<InscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<InscricaoDto>.Failure(erroDono);
        }

        return await Services.PreparacaoDaAvaliacaoDaInscricao
            .ProjetarInscricaoGravadaAsync(
                inscricao, certameDivulgadoRepository, processoSeletivoRepository, documentoRepository, registroCodecs, resolvedorFuso, conferenciaEnderecoGeo, enderecoNoAcervo,
                timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }
}

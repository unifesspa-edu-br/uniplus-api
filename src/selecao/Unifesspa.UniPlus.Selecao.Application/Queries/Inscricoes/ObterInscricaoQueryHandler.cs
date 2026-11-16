namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Devolve a inscrição do dono do token — nunca de outro CPF, mesmo que o <c>contaId</c> coincida por
/// engano de emissor (<see cref="Inscricao.ConferirDono"/> confere o <c>contaId</c> gravado) —, com a
/// avaliação do que ela gravou.
/// </summary>
public static class ObterInscricaoQueryHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        ObterInscricaoQuery query,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        ConsultaDaInscricaoDoCandidato consulta,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(query.ProcessoSeletivoId, userContext, "ler a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<InscricaoDto>.Failure(inscricao.Error!)
            : await consulta.ProjetarAsync(inscricao.Value!, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }
}

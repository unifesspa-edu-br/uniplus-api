namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Avalia, sem gravar, as respostas que o dono do token tem na tela, mescladas ao que a inscrição
/// gravou. A consulta é prévia: nunca recusa por restrição, obrigatoriedade ou opção fora da oferta —
/// isso é do envio —, só pela forma do corpo, quando uma ocorrência de grupo não tem identidade
/// própria. Responde só ao titular, dentro do período da inscrição, como o rascunho.
/// </summary>
public static class AvaliarInscricaoQueryHandler
{
    public static async Task<Result<AvaliacaoDaInscricaoDto>> Handle(
        AvaliarInscricaoQuery query,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        PeriodoDaInscricao periodo,
        AvaliacaoDaInscricaoDoCandidato avaliacao,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(query.ProcessoSeletivoId, userContext, "consultar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<AvaliacaoDaInscricaoDto>.Failure(inscricao.Error!)
            : await AvaliarNoPeriodoAsync(query, inscricao.Value!, periodo, avaliacao, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarNoPeriodoAsync(
        AvaliarInscricaoQuery query,
        Inscricao inscricao,
        PeriodoDaInscricao periodo,
        AvaliacaoDaInscricaoDoCandidato avaliacao,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        // O mesmo instante confere o período e é o envio de que se contam os limites da faixa de data.
        DomainError? foraDoPeriodo = await periodo.ConferirAsync(query.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false);
        return foraDoPeriodo is not null
            ? Result<AvaliacaoDaInscricaoDto>.Failure(foraDoPeriodo)
            : await avaliacao.AvaliarAsync(inscricao, query, instante, cancellationToken).ConfigureAwait(false);
    }
}

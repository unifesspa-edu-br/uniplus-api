namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;

using DTOs.Comprovantes;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Envia ou corrige a inscrição (issue #1859): a avaliação completa decide o veredito, nunca a
/// gravação — ou tudo passa, ou nada é persistido. Diferente de <see cref="SalvarInscricaoCommandHandler"/>,
/// a avaliação roda sobre o estado FINAL (o que já estava gravado, mesclado com o corpo recebido —
/// nunca só o delta desta chamada), porque a submissão exige obrigatórios do formulário inteiro.
/// </summary>
public static class SubmeterInscricaoCommandHandler
{
    public static async Task<Result<ReciboDaInscricaoDto>> Handle(
        SubmeterInscricaoCommand command,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        PeriodoDaInscricao periodo,
        EnvioDaInscricao envio,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(command.ProcessoSeletivoId, userContext, "submeter a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<ReciboDaInscricaoDto>.Failure(inscricao.Error!)
            : await EnviarNoPeriodoAsync(command, inscricao.Value!, periodo, envio, userContext, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ReciboDaInscricaoDto>> EnviarNoPeriodoAsync(
        SubmeterInscricaoCommand command,
        Inscricao inscricao,
        PeriodoDaInscricao periodo,
        EnvioDaInscricao envio,
        IUserContext userContext,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        // Um instante só para o envio inteiro: confere o período, é o dia de que se contam os limites da faixa de data e fica no
        // comprovante — quem reavaliar a inscrição pelo comprovante chega ao mesmo veredito, inclusive na virada do dia.
        DomainError? foraDoPeriodo = await periodo.ConferirAsync(command.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false);
        return foraDoPeriodo is not null
            ? Result<ReciboDaInscricaoDto>.Failure(foraDoPeriodo)
            : await envio.EnviarAsync(inscricao, command, userContext, instante, cancellationToken).ConfigureAwait(false);
    }
}

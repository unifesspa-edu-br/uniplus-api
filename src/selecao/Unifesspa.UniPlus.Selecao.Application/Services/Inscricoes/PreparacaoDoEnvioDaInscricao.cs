namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Commands.Inscricoes;

using Domain.Entities;

using DTOs;

using Kernel.Results;

/// <summary>Fixa o instante e a versão do envio antes de ler as declarações do candidato.</summary>
public sealed class PreparacaoDoEnvioDaInscricao(
    PeriodoDaInscricaoResolver periodo,
    VersaoDaInscricaoResolver versao,
    ContextoDaVersaoFixadaResolver contexto,
    TimeProvider relogio)
{
    public async Task<Result<EnvioPreparado>> PrepararAsync(
        Inscricao inscricao, SubmeterInscricaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(command);
        DateTimeOffset instante = relogio.GetUtcNow();
        DomainError? recusa = (await periodo.ConferirAsync(command.ProcessoSeletivoId, instante, cancellationToken).ConfigureAwait(false))?.Recusa;
        return recusa is not null
            ? Result<EnvioPreparado>.Failure(recusa)
            : await PrepararNaVersaoAsync(inscricao, command, instante, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioPreparado>> PrepararNaVersaoAsync(
        Inscricao inscricao, SubmeterInscricaoCommand command, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<EnvioPreparado>.Failure(adotada.Error!);
        }

        Result<ContextoDaVersaoFixada> fixado = await contexto.ResolverAsync(
            inscricao.Formulario.VersaoFormularioId, command.ProcessoSeletivoId,
            command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
        return fixado.IsFailure
            ? Result<EnvioPreparado>.Failure(fixado.Error!)
            : LerCorpo(inscricao, command, instante, fixado.Value!);
    }

    private static Result<EnvioPreparado> LerCorpo(
        Inscricao inscricao, SubmeterInscricaoCommand command, DateTimeOffset instante, ContextoDaVersaoFixada contexto)
    {
        Result<CorpoDoRascunho> corpo = CorpoDoRascunho.De(command.Request);
        return corpo.IsFailure
            ? Result<EnvioPreparado>.ValidationFailure(corpo.Errors)
            : Result<EnvioPreparado>.Success(new EnvioPreparado(inscricao, contexto, instante, command.Request, corpo.Value!));
    }
}

/// <summary>As declarações recebidas, ligadas à versão fixada e ao instante que conferiu o período.</summary>
public sealed record EnvioPreparado(
    Inscricao Inscricao, ContextoDaVersaoFixada Contexto, DateTimeOffset Instante, InscricaoRequest Request, CorpoDoRascunho Recebido);

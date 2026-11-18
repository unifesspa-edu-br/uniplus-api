namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Commands.Inscricoes;

using Domain.Entities;

using DTOs.Comprovantes;

using Kernel.Results;

/// <summary>Encadeia as etapas do envio, interrompendo somente o que depende de uma etapa recusada.</summary>
public sealed class SubmissaoDaInscricao(
    PreparacaoDoEnvioDaInscricao preparacao,
    ConferenciaDoEnvioDaInscricao conferencia,
    RegistroDoEnvioDaInscricao registro)
{
    public async Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        Inscricao inscricao, SubmeterInscricaoCommand command, CancellationToken cancellationToken)
    {
        Result<EnvioPreparado> preparado = await preparacao.PrepararAsync(inscricao, command, cancellationToken).ConfigureAwait(false);
        if (preparado.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(preparado.Errors);
        }
        Result<EnvioConferido> conferido = await conferencia.ConferirAsync(preparado.Value!, cancellationToken).ConfigureAwait(false);
        return conferido.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(conferido.Errors)
            : await registro.RegistrarAsync(conferido.Value!, command.Precondicao, cancellationToken).ConfigureAwait(false);
    }

}

namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Consulta um ato do processo. O ato de outro processo responde como inexistente: a consulta não revela
/// que o identificador existe fora do processo da rota.
/// </summary>
public static class ObterAtoDoProcessoQueryHandler
{
    public static async Task<Result<AtoDoProcessoDto>> Handle(
        ObterAtoDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);

        Result<AtoDoProcesso> ato = await LocalizarAsync(query, processoSeletivoRepository, atoDoProcessoRepository, cancellationToken)
            .ConfigureAwait(false);

        return ato.IsFailure
            ? Result<AtoDoProcessoDto>.Failure(ato.Error!)
            : Result<AtoDoProcessoDto>.Success(await ProjecaoDosAtosDoProcesso
                .ProjetarUmAsync(query.ProcessoSeletivoId, ato.Value!, documentoAPublicarRepository, cancellationToken)
                .ConfigureAwait(false));
    }

    private static async Task<Result<AtoDoProcesso>> LocalizarAsync(
        ObterAtoDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        CancellationToken cancellationToken)
    {
        if (!await processoSeletivoRepository.ExisteAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false))
        {
            return Result<AtoDoProcesso>.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        AtoDoProcesso? ato = await atoDoProcessoRepository.ObterComAnexosAsync(query.AtoId, cancellationToken).ConfigureAwait(false);

        return ato is not null && ato.ProcessoSeletivoId == query.ProcessoSeletivoId
            ? Result<AtoDoProcesso>.Success(ato)
            : Result<AtoDoProcesso>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.NaoEncontrado,
                "Ato do processo não encontrado."));
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs.Comprovantes;

using Kernel.Results;

using Services;
using Services.Comprovantes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Devolve o comprovante de um envio da inscrição, pronto para exibir. A ordem das recusas é fixa: o dono primeiro, para que a
/// inscrição alheia responda como inexistente; depois a inscrição cancelada, que não tem comprovante; só então se lê e se decifra
/// o comprovante pedido, e nenhum outro.
/// </summary>
public static class ObterComprovanteDaInscricaoQueryHandler
{
    public static async Task<Result<ComprovanteDaInscricaoDto>> Handle(
        ObterComprovanteDaInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        MontadorDoComprovante montador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(montador);

        Result<Inscricao> dona = await InscricaoDoDono
            .ObterAsync(query.InscricaoId, inscricaoRepository, userContext, cancellationToken).ConfigureAwait(false);

        return dona.IsFailure
            ? Result<ComprovanteDaInscricaoDto>.Failure(dona.Error!)
            : await LerAsync(dona.Value!, query.Numero, inscricaoRepository, montador, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ComprovanteDaInscricaoDto>> LerAsync(
        Inscricao inscricao, int numero, IInscricaoRepository inscricaoRepository, MontadorDoComprovante montador, CancellationToken cancellationToken)
    {
        if (inscricao.ConferirComprovanteEmitivel() is { } cancelada)
        {
            return Result<ComprovanteDaInscricaoDto>.Failure(cancelada);
        }

        return await ComprovanteDoNumero.MontarAsync(inscricao, numero, inscricaoRepository, montador, cancellationToken).ConfigureAwait(false);
    }
}

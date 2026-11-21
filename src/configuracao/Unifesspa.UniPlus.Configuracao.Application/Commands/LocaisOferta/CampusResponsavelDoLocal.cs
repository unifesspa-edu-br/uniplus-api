namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Regra do campus responsável do local de oferta: o campus informado precisa existir e estar vivo (FK intra-banco
/// opcional, ADR-0065).
/// </summary>
internal static class CampusResponsavelDoLocal
{
    public static async Task<Result> ConferirAsync(
        Guid campusResponsavelId, ICampusRepository campusRepository, CancellationToken cancellationToken)
    {
        bool existe = await campusRepository.ExisteVivoAsync(campusResponsavelId, cancellationToken).ConfigureAwait(false);

        return existe
            ? Result.Success()
            : Result.Failure(new DomainError(
                LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
                "O Campus responsável informado não foi encontrado."));
    }
}

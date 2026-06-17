namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarLocalOfertaCommand"/>: confere a existência do
/// campus responsável (quando informado, FK intra-banco opcional ADR-0065),
/// cria o agregado carimbando a proveniência do display cache e persiste.
/// </summary>
public static class CriarLocalOfertaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarLocalOfertaCommand command,
        ILocalOfertaRepository repository,
        ICampusRepository campusRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(campusRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (command.CampusResponsavelId.HasValue
            && !await campusRepository.ExisteVivoAsync(command.CampusResponsavelId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
                "O Campus responsável informado não foi encontrado."));
        }

        Result<LocalOferta> localResult = LocalOferta.Criar(
            command.Tipo,
            command.CampusResponsavelId,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            ReferenciaCidadeGeo.OrigemGeoApi,
            timeProvider.GetUtcNow(),
            command.Endereco,
            command.CodigoEmec);

        if (localResult.IsFailure)
        {
            return Result<Guid>.Failure(localResult.Error!);
        }

        LocalOferta local = localResult.Value!;
        await repository.AdicionarAsync(local, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(local.Id);
    }
}

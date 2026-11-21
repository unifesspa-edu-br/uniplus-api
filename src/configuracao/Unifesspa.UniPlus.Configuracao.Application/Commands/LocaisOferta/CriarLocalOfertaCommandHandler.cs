namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarLocalOfertaCommand"/>: valida o agregado por
/// inteiro primeiro (sem I/O) — tipo, cidade, coerência com o endereço e código
/// e-MEC acumulados na mesma resposta — só então confere a existência do campus
/// responsável (quando informado, FK intra-banco opcional ADR-0065) e persiste o
/// local, que define sozinho a proveniência da cidade.
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

        Result<LocalOferta> local = MontarLocal(command, timeProvider.GetUtcNow());

        return local.Value is { } novo
            ? await ConferirCampusEGravarAsync(novo, repository, campusRepository, unitOfWork, cancellationToken).ConfigureAwait(false)
            : Result<Guid>.ValidationFailure(local.Errors);
    }

    // Não retorna cedo quando o endereço falha: o local é criado de qualquer forma (sem endereço) para que tipo, cidade e
    // código e-MEC também sejam conferidos e entrem juntos em errors[]; senão um CEP malformado esconderia os demais.
    private static Result<LocalOferta> MontarLocal(CriarLocalOfertaCommand command, DateTimeOffset agora)
    {
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result<LocalOferta> local = LocalOferta.Criar(command.Declaracao(enderecoErro is null ? endereco : null), agora);

        return enderecoErro is null
            ? local
            : Result<LocalOferta>.ValidationFailure([.. local.Errors, new FieldError("endereco", enderecoErro)]);
    }

    private static async Task<Result<Guid>> ConferirCampusEGravarAsync(
        LocalOferta local,
        ILocalOfertaRepository repository,
        ICampusRepository campusRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result campus = local.CampusResponsavelId is { } campusId
            ? await CampusResponsavelDoLocal.ConferirAsync(campusId, campusRepository, cancellationToken).ConfigureAwait(false)
            : Result.Success();

        return campus.IsFailure
            ? Result<Guid>.ValidationFailure(campus.Errors)
            : await GravarAsync(local, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> GravarAsync(
        LocalOferta local, ILocalOfertaRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(local, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(local.Id);
    }
}

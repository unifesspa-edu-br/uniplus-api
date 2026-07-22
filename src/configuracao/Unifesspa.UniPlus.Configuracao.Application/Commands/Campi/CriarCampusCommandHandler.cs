namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCampusCommand"/> (convention-based Wolverine):
/// confere a unicidade da sigla entre campi vivos, cria o agregado carimbando a
/// proveniência do display cache (<c>cidade_origem = "geo-api"</c>) + instante a
/// partir do <see cref="TimeProvider"/>, persiste e commita.
/// </summary>
public static class CriarCampusCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCampusCommand command,
        ICampusRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (await repository.SiglaExisteEntreLivosAsync(command.Sigla, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                CampusErrorCodes.SiglaJaExiste,
                $"Já existe um Campus vivo com a sigla '{command.Sigla}'."));
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);
        if (enderecoErro is not null)
        {
            return Result<Guid>.Failure(enderecoErro);
        }

        Result<Campus> campusResult = Campus.Criar(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            ReferenciaCidadeGeo.OrigemGeoApi,
            agora,
            endereco,
            command.CodigoEmec);

        if (campusResult.IsFailure)
        {
            return Result<Guid>.Failure(campusResult.Error!);
        }

        Campus campus = campusResult.Value!;
        await repository.AdicionarAsync(campus, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(campus.Id);
    }
}

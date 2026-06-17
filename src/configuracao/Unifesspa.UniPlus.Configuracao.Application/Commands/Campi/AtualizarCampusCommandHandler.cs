namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarCampusCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCampusCommand command,
        ICampusRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Campus? campus = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NaoEncontrado,
                "Campus não encontrado."));
        }

        // Sigla normaliza para uppercase no agregado e o índice único é case-insensitive
        // (comparação OrdinalIgnoreCase) — só checa colisão quando a sigla muda.
        if (!string.Equals(command.Sigla.Trim(), campus.Sigla, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(command.Sigla, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.SiglaJaExiste,
                $"Já existe um Campus vivo com a sigla '{command.Sigla}'."));
        }

        Result atualizarResult = campus.Atualizar(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            ReferenciaCidadeGeo.OrigemGeoApi,
            timeProvider.GetUtcNow(),
            command.Endereco,
            command.Cep,
            command.Latitude,
            command.Longitude,
            command.CodigoEmec);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

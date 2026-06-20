namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarLocalOfertaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarLocalOfertaCommand command,
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

        LocalOferta? local = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (local is null)
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.NaoEncontrado,
                "Local de Oferta não encontrado."));
        }

        if (command.CampusResponsavelId.HasValue
            && command.CampusResponsavelId != local.CampusResponsavelId
            && !await campusRepository.ExisteVivoAsync(command.CampusResponsavelId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
                "O Campus responsável informado não foi encontrado."));
        }

        // Só recarimba a proveniência/frescura do display cache quando o trio de
        // cidade efetivamente muda (mesma semântica do Campus).
        bool cidadeMudou = CidadeReferenciaMudou(command, local);
        string? cidadeOrigem = cidadeMudou ? ReferenciaCidadeGeo.OrigemGeoApi : local.CidadeOrigem;
        DateTimeOffset? cidadeAtualizadoEm = cidadeMudou ? timeProvider.GetUtcNow() : local.CidadeDisplayAtualizadoEm;

        Result atualizarResult = local.Atualizar(
            command.Tipo,
            command.CampusResponsavelId,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            command.Endereco,
            command.CodigoEmec);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static bool CidadeReferenciaMudou(AtualizarLocalOfertaCommand command, LocalOferta local) =>
        !string.Equals(command.CidadeCodigoIbge.Trim(), local.CidadeCodigoIbge, StringComparison.Ordinal)
        || !string.Equals(command.CidadeNome.Trim(), local.CidadeNome, StringComparison.Ordinal)
        || !string.Equals(command.CidadeUf.Trim(), local.CidadeUf, StringComparison.OrdinalIgnoreCase);
}

namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverCampusCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia quando o campus é responsável por
/// algum <c>LocalOferta</c> vivo (integridade do vínculo intra-banco, ADR-0065).
/// </summary>
public static class RemoverCampusCommandHandler
{
    public static async Task<Result> Handle(
        RemoverCampusCommand command,
        ICampusRepository repository,
        ILocalOfertaRepository localOfertaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(localOfertaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Campus? campus = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NaoEncontrado,
                "Campus não encontrado."));
        }

        // Check-then-act, simétrico ao bloqueio de remoção da Unidade no pilot.
        // Sob concorrência (remoção do campus × criação de LocalOferta que o aponta)
        // a checagem pode ver `false` antes do commit; a serialização estrita é
        // controle cross-cutting comum a todos os cadastros, fora desta Story.
        if (await localOfertaRepository.ExisteVivoComCampusResponsavelAsync(command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.RemocaoBloqueadaPorLocalOferta,
                "Não é possível remover um Campus que é responsável por Locais de Oferta ativos."));
        }

        repository.Remover(campus);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

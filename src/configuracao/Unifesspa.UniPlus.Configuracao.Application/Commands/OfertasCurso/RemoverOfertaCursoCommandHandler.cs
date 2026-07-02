namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverOfertaCursoCommand"/>. Soft-delete simples via
/// <c>SoftDeleteInterceptor</c> — a remoção <b>não</b> é bloqueada por snapshots
/// externos: cópias congeladas da oferta em outros módulos (ex.: edital de
/// Seleção, ADR-0061) são desacopladas e preservam o histórico por conta
/// própria. Só a oferta viva sai das leituras.
/// </summary>
public static class RemoverOfertaCursoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverOfertaCursoCommand command,
        IOfertaCursoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        OfertaCurso? oferta = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (oferta is null)
        {
            return Result.Failure(new DomainError(
                OfertaCursoErrorCodes.NaoEncontrada,
                "Oferta de curso não encontrada."));
        }

        repository.Remover(oferta);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

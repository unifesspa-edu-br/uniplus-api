namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="RemoverTipoAtoPublicadoCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. A remoção nunca é bloqueada: o ato publicado copia
/// os atributos do tipo por valor no instante da publicação (ADR-0103), de modo que
/// nenhum ato passa a apontar para um tipo ausente.
/// </summary>
public static class RemoverTipoAtoPublicadoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IPublicacoesUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoAtoPublicado? tipo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.NaoEncontrado,
                "Tipo de ato não encontrado."));
        }

        repository.Remover(tipo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

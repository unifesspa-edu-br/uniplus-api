namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverLocalOfertaCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia quando o local é referenciado por
/// oferta de curso viva (UNI-REQ-0010). A entidade <c>oferta_curso</c> ainda não
/// existe; a checagem é ponto de extensão (retorna <see langword="false"/>).
/// </summary>
public static class RemoverLocalOfertaCommandHandler
{
    public static async Task<Result> Handle(
        RemoverLocalOfertaCommand command,
        ILocalOfertaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        LocalOferta? local = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (local is null)
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.NaoEncontrado,
                "Local de Oferta não encontrado."));
        }

        // Ponto de extensão UNI-REQ-0010: oferta_curso ainda não existe no módulo,
        // então a checagem retorna false hoje. Quando a oferta de curso chegar, o
        // bloqueio passa a valer sem mudar este handler.
        if (await repository.ReferenciadoPorOfertaCursoVivaAsync(command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso,
                "Não é possível remover um Local de Oferta referenciado por oferta de curso ativa."));
        }

        repository.Remover(local);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

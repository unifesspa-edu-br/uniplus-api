namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverCursoCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia quando o curso é referenciado por
/// oferta de curso viva. A entidade <c>oferta_curso</c> ainda não existe (#749);
/// a checagem é ponto de extensão (retorna <see langword="false"/>).
/// </summary>
public static class RemoverCursoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverCursoCommand command,
        ICursoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Curso? curso = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (curso is null)
        {
            return Result.Failure(new DomainError(
                CursoErrorCodes.NaoEncontrado,
                "Curso não encontrado."));
        }

        // Ponto de extensão #749: oferta_curso ainda não existe no módulo, então a
        // checagem retorna false hoje. Quando a oferta de curso chegar, o bloqueio
        // passa a valer sem mudar este handler.
        if (await repository.ReferenciadoPorOfertaCursoVivaAsync(command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                CursoErrorCodes.RemocaoBloqueadaPorOfertaCurso,
                "Não é possível remover um Curso referenciado por oferta de curso ativa."));
        }

        repository.Remover(curso);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

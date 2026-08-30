namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverCategoriaDocumentoCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>, que libera o código para novo cadastro. A remoção
/// nunca é bloqueada por referência: o consumo cross-módulo é snapshot-copy
/// desacoplado (ADR-0061) e o edital já publicado carrega a categoria congelada
/// por valor.
/// </summary>
public static class RemoverCategoriaDocumentoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverCategoriaDocumentoCommand command,
        ICategoriaDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CategoriaDocumento? categoria = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (categoria is null)
        {
            return Result.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.NaoEncontrada,
                "Categoria de documento não encontrada."));
        }

        repository.Remover(categoria);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

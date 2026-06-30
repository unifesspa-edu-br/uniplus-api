namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverRecursoAcessibilidadeCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. A remoção <b>nunca</b> é bloqueada: o consumo
/// cross-módulo é snapshot-copy desacoplado (ADR-0061) — remover um recurso
/// referenciado por valor numa configuração de Seleção apenas deixa o rótulo já
/// congelado intacto, sem alvo vivo.
/// </summary>
public static class RemoverRecursoAcessibilidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverRecursoAcessibilidadeCommand command,
        IRecursoAcessibilidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        RecursoAcessibilidade? recurso = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (recurso is null)
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.NaoEncontrado,
                "Recurso de acessibilidade não encontrado."));
        }

        repository.Remover(recurso);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

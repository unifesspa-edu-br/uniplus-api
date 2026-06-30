namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverTipoDeficienciaCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. A remoção <b>nunca</b> é bloqueada: o consumo
/// cross-módulo é snapshot-copy desacoplado (ADR-0061), então remover um tipo de
/// deficiência não afeta rótulos já congelados em outros contextos.
/// </summary>
public static class RemoverTipoDeficienciaCommandHandler
{
    public static async Task<Result> Handle(
        RemoverTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoDeficiencia? tipo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NaoEncontrado,
                "Tipo de deficiência não encontrado."));
        }

        repository.Remover(tipo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

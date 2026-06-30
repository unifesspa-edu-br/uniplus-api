namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverTipoDocumentoCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. A remoção <b>nunca</b> é bloqueada: o consumo
/// cross-módulo é snapshot-copy desacoplado (ADR-0061) e o <c>TipoEquivalente</c>
/// de outro tipo é rótulo classificatório por código (não FK), não referência
/// viva — remover um tipo apontado como equivalente apenas deixa o rótulo do outro
/// apontando para um código sem alvo vivo (CA-04).
/// </summary>
public static class RemoverTipoDocumentoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverTipoDocumentoCommand command,
        ITipoDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoDocumento? tipo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDocumentoErrorCodes.NaoEncontrado,
                "Tipo de documento não encontrado."));
        }

        repository.Remover(tipo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

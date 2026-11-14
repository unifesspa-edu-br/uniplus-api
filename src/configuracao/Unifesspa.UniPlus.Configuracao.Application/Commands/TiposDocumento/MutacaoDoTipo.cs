namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O fluxo comum das edições de um tipo de documento existente: carregar, aplicar a mutação do agregado e gravar,
/// traduzindo a corrida de concorrência otimista (xmin) em conflito. A gravação sozinha serve também a quem muda mais
/// de um item de uma vez.
/// </summary>
internal static class MutacaoDoTipo
{
    public static async Task<Result> AplicarAsync(
        Guid id,
        Func<TipoDocumento, Result> mutacao,
        ITipoDocumentoRepository repository,
        IUnitOfWork unitOfWork,
        DomainError conflito,
        CancellationToken cancellationToken)
    {
        TipoDocumento? tipo = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoDocumentoErrorCodes.NaoEncontrado, "Tipo de documento não encontrado."));
        }

        Result resultado = mutacao(tipo);
        return resultado.IsFailure
            ? resultado
            : await SalvarAsync(unitOfWork, conflito, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Grava e traduz o conflito de concorrência em <paramref name="conflito"/>. Os endpoints têm Idempotency-Key
    /// (ADR-0119): a captura é local, com descarte, para o <c>SaveChangesAsync</c> do outbox não reencontrar a entidade
    /// modificada. Qualquer outra exceção sobe para quem chama.
    /// </summary>
    public static async Task<Result> SalvarAsync(
        IUnitOfWork unitOfWork, DomainError conflito, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(conflito);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(conflito);
        }

        return Result.Success();
    }
}

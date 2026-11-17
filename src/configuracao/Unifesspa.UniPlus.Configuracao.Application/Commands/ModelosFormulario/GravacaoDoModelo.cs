namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação do modelo, que a criação e a edição têm em comum: traduz as corridas de índice único
/// e de concorrência otimista (xmin) em recusa.
/// </summary>
internal static class GravacaoDoModelo
{
    private const string IndiceUnicoDoCodigo = "ix_modelos_formulario_codigo";

    /// <summary>Grava o modelo novo: o código é único entre todos os modelos, desativados inclusive.</summary>
    public static async Task<Result<Guid>> GravarNovoAsync(
        ModeloFormulario modelo, IModeloFormularioRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(modelo.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste());
        }

        await repository.AdicionarAsync(modelo, cancellationToken).ConfigureAwait(false);
        bool gravou = await SalvarTraduzindoCorridaAsync(
            unitOfWork, static exception => UniqueConstraintViolation.EhConflito(exception, IndiceUnicoDoCodigo), cancellationToken).ConfigureAwait(false);
        return gravou ? Result<Guid>.Success(modelo.Id) : Result<Guid>.Failure(CodigoJaExiste());
    }

    /// <summary>Carrega o modelo, aplica a mutação e grava, traduzindo a corrida de concorrência em conflito.</summary>
    public static async Task<Result> MutarAsync(
        Guid id,
        Func<ModeloFormulario, Task<Result>> mutacao,
        IModeloFormularioRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ModeloFormulario? modelo = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.NaoEncontrado, "Modelo de formulário não encontrado."));
        }

        return await MutarEGravarAsync(modelo, mutacao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> MutarEGravarAsync(
        ModeloFormulario modelo, Func<ModeloFormulario, Task<Result>> mutacao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = await mutacao(modelo).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            // A edição recusada pelo catálogo pode já ter mudado o modelo rastreado; sem descartar,
            // o SaveChangesAsync do outbox o gravaria mesmo com a recusa.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return resultado;
        }

        bool gravou = await SalvarTraduzindoCorridaAsync(unitOfWork, OptimisticConcurrencyViolation.Is, cancellationToken).ConfigureAwait(false);
        return gravou ? Result.Success() : Result.Failure(new DomainError(
            ModeloFormularioErrorCodes.ConflitoDeConcorrencia, "Este modelo de formulário foi alterado concorrentemente. Tente novamente."));
    }

    /// <summary>
    /// Salva, e devolve <see langword="false"/> quando a exceção é a corrida esperada. A captura é
    /// local, com o descarte do rastreamento (os endpoints têm Idempotency-Key, ADR-0119): sem
    /// descartar, o SaveChangesAsync do outbox repete a gravação fora daqui, e o 409 vira 500.
    /// </summary>
    private static async Task<bool> SalvarTraduzindoCorridaAsync(
        IUnitOfWork unitOfWork, Func<Exception, bool> ehACorrida, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (ehACorrida(exception))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return false;
        }
    }

    private static DomainError CodigoJaExiste() => new(
        ModeloFormularioErrorCodes.CodigoJaExiste, "Já existe um modelo de formulário com o código informado.");
}

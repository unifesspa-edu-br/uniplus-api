namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Executa uma operação sobre o perfil na ordem de travas única: localiza e trava o perfil (<c>FOR UPDATE</c>),
/// recarrega-o por inteiro, deixa a operação validar e fazer a transição e só então grava. Duas operações sobre o mesmo
/// perfil, ou sobre um documento dele, nunca correm juntas: a segunda lê o que a primeira confirmou.
/// </summary>
public sealed class OperacaoSobreOPerfil
{
    private readonly IPerfilRepository _perfis;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;

    public OperacaoSobreOPerfil(IPerfilRepository perfis, IAutorizacaoUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _perfis = perfis;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<T>> ExecutarAsync<T>(
        Guid perfilId, Func<Perfil, Task<Result<T>>> operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        Perfil? perfil = await _perfis.ObterParaEditarAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<T>.Failure(PerfilHandlerErros.NaoEncontrado(perfilId));
        }

        Result<T> resultado = await operacao(perfil).ConfigureAwait(false);

        return resultado.IsSuccess
            ? await SalvarAsync(resultado, cancellationToken).ConfigureAwait(false)
            : Descartar(resultado);
    }

    /// <summary>A mesma operação, para as que só mudam o estado e não devolvem nada.</summary>
    public async Task<Result> ExecutarAsync(Guid perfilId, Func<Perfil, Result> operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        Result<bool> resultado = await ExecutarAsync(
            perfilId,
            perfil => Task.FromResult(Converter(operacao(perfil))),
            cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? Result.Success() : Result.ValidationFailure(resultado.Errors);
    }

    /// <summary>Grava o perfil novo. A unicidade do nome é conferida antes; o índice único barra a corrida.</summary>
    public async Task<Result<T>> AdicionarAsync<T>(Perfil perfil, Result<T> resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(resultado);

        await _perfis.AdicionarAsync(perfil, cancellationToken).ConfigureAwait(false);

        return await SalvarAsync(resultado, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<T>> SalvarAsync<T>(Result<T> resultado, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return resultado;
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeNome(ex))
        {
            return Descartar(Result<T>.Failure(PerfilErros.NomeDuplicado()));
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119): a segunda operação recebe o
            // conflito em vez de sobrescrever a primeira.
            return Descartar(Result<T>.Failure(PerfilHandlerErros.ConflitoDeConcorrencia()));
        }
    }

    private Result<T> Descartar<T>(Result<T> resultado)
    {
        _unitOfWork.DescartarAlteracoesNaoSalvas();

        return resultado;
    }

    private static Result<bool> Converter(Result resultado) =>
        resultado.IsSuccess ? Result<bool>.Success(true) : Result<bool>.ValidationFailure(resultado.Errors);
}

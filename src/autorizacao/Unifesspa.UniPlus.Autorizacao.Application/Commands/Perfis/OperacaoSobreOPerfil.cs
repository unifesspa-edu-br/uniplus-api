namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que uma operação sobre o perfil decidiu: o resultado, os avisos que a gravação dela pede e se o perfil precisa ser
/// gravado ainda que o resultado seja uma recusa, como o fim do prazo materializado pela própria operação.
/// </summary>
/// <param name="Resultado">O resultado da operação.</param>
/// <param name="GravarMesmoNaFalha">O perfil mudou e a mudança vale mesmo com a recusa.</param>
/// <param name="Avisos">Os e-mails que entram no outbox junto da gravação; só saem se ela acontece.</param>
public sealed record DesfechoDaOperacao<T>(Result<T> Resultado, bool GravarMesmoNaFalha = false, IReadOnlyList<EnviarEmail>? Avisos = null);

/// <summary>
/// O que a operação sabe do momento em que roda sob a trava: o instante lido depois de obtê-la e a versão cujo prazo
/// venceu e foi materializado antes dela, quando houve.
/// </summary>
public sealed record ContextoDaOperacao(DateTimeOffset Agora, Guid? VersaoExpirada);

/// <summary>O resultado da operação depois da gravação e os avisos que entram no outbox da mesma transação.</summary>
public sealed record GravacaoDaOperacao<T>(Result<T> Resultado, IReadOnlyList<EnviarEmail> Avisos)
{
    public static GravacaoDaOperacao<T> Sem(Result<T> resultado) => new(resultado, []);
}

/// <summary>
/// Executa uma operação sobre o perfil na ordem de travas única: localiza e trava o perfil (<c>FOR UPDATE</c>),
/// recarrega-o por inteiro, materializa o prazo vencido da pendente, deixa a operação validar e fazer a transição e só
/// então grava. O instante da operação é lido depois da trava, e a expiração materializada grava mesmo que a operação
/// seguinte recuse. Duas operações sobre o mesmo perfil, ou sobre um documento dele, nunca correm juntas: a segunda lê o
/// que a primeira confirmou.
/// </summary>
public sealed class OperacaoSobreOPerfil
{
    private readonly IPerfilRepository _perfis;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public OperacaoSobreOPerfil(IPerfilRepository perfis, IAutorizacaoUnitOfWork unitOfWork, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// A operação sobre o perfil, ou sobre uma versão dele quando <paramref name="versaoId"/> a informa: toda recusa
    /// sobre a versão que acabou de vencer é a do prazo.
    /// </summary>
    public async Task<Result<T>> ExecutarAsync<T>(
        Guid perfilId, Guid? versaoId, Func<Perfil, ContextoDaOperacao, Task<Result<T>>> operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        GravacaoDaOperacao<T> gravacao = await ExecutarComAvisosAsync(
            perfilId,
            versaoId,
            async (perfil, contexto) => new DesfechoDaOperacao<T>(await operacao(perfil, contexto).ConfigureAwait(false)),
            cancellationToken)
            .ConfigureAwait(false);

        return gravacao.Resultado;
    }

    /// <summary>A mesma operação, para as que só mudam o estado e não devolvem nada.</summary>
    public async Task<Result> ExecutarAsync(Guid perfilId, Func<Perfil, Result> operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        Result<bool> resultado = await ExecutarAsync(
            perfilId,
            null,
            (perfil, _) => Task.FromResult(Converter(operacao(perfil))),
            cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? Result.Success() : Result.ValidationFailure(resultado.Errors);
    }

    /// <summary>
    /// A operação que pode gravar mesmo recusando e que devolve avisos. A recusa que grava leva o perfil e os avisos dela
    /// à mesma transação; a que não grava descarta tudo.
    /// </summary>
    public async Task<GravacaoDaOperacao<T>> ExecutarComAvisosAsync<T>(
        Guid perfilId,
        Guid? versaoId,
        Func<Perfil, ContextoDaOperacao, Task<DesfechoDaOperacao<T>>> operacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        Perfil? perfil = await _perfis.ObterParaEditarAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return GravacaoDaOperacao<T>.Sem(Result<T>.Failure(PerfilHandlerErros.NaoEncontrado(perfilId)));
        }

        ContextoDaOperacao contexto = MaterializarOPrazo(perfil);
        DesfechoDaOperacao<T> desfecho = ConsiderarOPrazo(
            await operacao(perfil, contexto).ConfigureAwait(false), contexto, versaoId);

        return desfecho.Resultado.IsSuccess || desfecho.GravarMesmoNaFalha
            ? await SalvarAsync(desfecho, cancellationToken).ConfigureAwait(false)
            : Descartar(desfecho.Resultado);
    }

    /// <summary>Grava o perfil novo. A unicidade do nome é conferida antes; o índice único barra a corrida.</summary>
    public async Task<Result<T>> AdicionarAsync<T>(Perfil perfil, Result<T> resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(resultado);

        await _perfis.AdicionarAsync(perfil, cancellationToken).ConfigureAwait(false);

        return (await SalvarAsync(new DesfechoDaOperacao<T>(resultado), cancellationToken).ConfigureAwait(false)).Resultado;
    }

    private ContextoDaOperacao MaterializarOPrazo(Perfil perfil)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        Guid? pendente = perfil.VersaoPendente?.Id;

        return new ContextoDaOperacao(agora, perfil.MaterializarExpiracao(agora) ? pendente : null);
    }

    private static DesfechoDaOperacao<T> ConsiderarOPrazo<T>(DesfechoDaOperacao<T> desfecho, ContextoDaOperacao contexto, Guid? versaoId)
    {
        if (contexto.VersaoExpirada is null)
        {
            return desfecho;
        }

        bool recusaDoPrazo = desfecho.Resultado.IsFailure && contexto.VersaoExpirada == versaoId;

        return desfecho with
        {
            Resultado = recusaDoPrazo ? Result<T>.Failure(PerfilErros.VersaoExpirada()) : desfecho.Resultado,
            GravarMesmoNaFalha = true,
        };
    }

    private async Task<GravacaoDaOperacao<T>> SalvarAsync<T>(DesfechoDaOperacao<T> desfecho, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return new GravacaoDaOperacao<T>(desfecho.Resultado, desfecho.Avisos ?? []);
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

    private GravacaoDaOperacao<T> Descartar<T>(Result<T> resultado)
    {
        _unitOfWork.DescartarAlteracoesNaoSalvas();

        return GravacaoDaOperacao<T>.Sem(resultado);
    }

    private static Result<bool> Converter(Result resultado) =>
        resultado.IsSuccess ? Result<bool>.Success(true) : Result<bool>.ValidationFailure(resultado.Errors);
}

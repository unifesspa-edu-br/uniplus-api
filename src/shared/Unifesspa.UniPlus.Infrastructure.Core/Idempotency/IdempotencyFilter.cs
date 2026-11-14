namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Reflection;

using Cryptography;

using Errors;

using Kernel.Results;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Resource filter (roda antes de model binding e antes do action filter) que implementa o protocolo Idempotency-Key
/// conforme ADR-0027 e draft-ietf-httpapi-idempotency-key-header-07. Faz lookup, replay verbatim em hit-match, 422 em
/// hit-mismatch, 409 em processing, 400 em key ausente/malformada. Em miss, reserva a entrada, deixa a action executar e
/// completa depois.
/// </summary>
/// <remarks>
/// <para><b>Por que ResourceFilter e não Middleware:</b> precisamos do metadata da action (atributo
/// <c>[RequiresIdempotencyKey]</c>), disponível após o routing e antes do model binding.</para>
/// <para><b>Atomicidade vs ADR-0027:</b> reserva e conclusão rodam em transações separadas (limitação documentada na ADR-0027
/// §"Negativas"). A janela de inconsistência é coberta pelo status Processing, que responde 409 a um retry concorrente.</para>
/// </remarks>
public sealed class IdempotencyFilter : IAsyncResourceFilter
{
    private readonly IIdempotencyStore _store;
    private readonly IUniPlusEncryptionService _encryption;
    private readonly IDomainErrorMapper _errorMapper;
    private readonly TimeProvider _time;
    private readonly IUserContext _userContext;
    private readonly IdempotencyOptions _options;
    private readonly IdempotentExecution _execution;

    public IdempotencyFilter(
        IIdempotencyStore store,
        IUniPlusEncryptionService encryption,
        IDomainErrorMapper errorMapper,
        TimeProvider time,
        IUserContext userContext,
        IOptions<IdempotencyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(errorMapper);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _encryption = encryption;
        _errorMapper = errorMapper;
        _time = time;
        _userContext = userContext;
        _options = options.Value;
        _execution = new IdempotentExecution(store, encryption);
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        RequiresIdempotencyKeyAttribute? attribute = AttributeOfAction(context);
        if (attribute is null)
        {
            await next().ConfigureAwait(false);
        }
        else
        {
            await ExecuteIdempotentAsync(context, next, attribute).ConfigureAwait(false);
        }
    }

    private async Task ExecuteIdempotentAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, RequiresIdempotencyKeyAttribute attribute)
    {
        Result<IdempotencyRequestKey> key = await IdempotencyRequestKeyReader
            .ReadAsync(context.HttpContext, _userContext, _options.MaxBodyBytes)
            .ConfigureAwait(false);
        if (key.IsFailure)
        {
            Refuse(context, key.Error!);
            return;
        }

        if (await ReserveAsync(context, key.Value!, attribute).ConfigureAwait(false))
        {
            await _execution.ExecuteAsync(context, next, key.Value!).ConfigureAwait(false);
        }
    }

    // Em miss, reserva a chave. Se a UNIQUE perder a corrida, relê: o vencedor pode já ter concluído (replay), ainda estar
    // em Processing (409) ou ter corpo diferente (422). Sem a releitura, o perdedor receberia 409 mesmo com o vencedor
    // pronto para replay, quebrando o contrato do draft IETF §3.
    private async Task<bool> ReserveAsync(ResourceExecutingContext context, IdempotencyRequestKey key, RequiresIdempotencyKeyAttribute attribute)
    {
        CancellationToken aborted = context.HttpContext.RequestAborted;

        if (await RespondFromCacheAsync(context, key, aborted).ConfigureAwait(false))
        {
            return false;
        }

        if (await TryReserveAsync(key, attribute, aborted).ConfigureAwait(false))
        {
            return true;
        }

        await RespondAfterRaceAsync(context, key, aborted).ConfigureAwait(false);
        return false;
    }

    private Task<bool> TryReserveAsync(IdempotencyRequestKey key, RequiresIdempotencyKeyAttribute attribute, CancellationToken cancellationToken)
    {
        TimeSpan validity = attribute.TtlSeconds >= 0 ? TimeSpan.FromSeconds(attribute.TtlSeconds) : _options.Ttl;
        return _store.TryReserveAsync(key.Scope, key.Endpoint, key.IdempotencyKey, key.BodyHash, _time.GetUtcNow().Add(validity), cancellationToken);
    }

    private Task<IdempotencyLookupResult> LookupAsync(IdempotencyRequestKey key, CancellationToken cancellationToken) =>
        _store.LookupAsync(key.Scope, key.Endpoint, key.IdempotencyKey, key.BodyHash, cancellationToken);

    private async Task RespondAfterRaceAsync(ResourceExecutingContext context, IdempotencyRequestKey key, CancellationToken cancellationToken)
    {
        if (await RespondFromCacheAsync(context, key, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Miss aqui só ocorre se a entrada foi apagada entre a reserva e a releitura (5xx do vencedor): raro, e o 409 com
        // retry imediato é o caminho seguro.
        Refuse(context, IdempotencyDomainErrorCodes.ProcessingConflict, "Request concorrente com mesma Idempotency-Key resolvida; tentar novamente.");
    }

    /// <summary>Responde a requisição pelo que o cache já sabe; <c>false</c> em miss, quando o handler deve rodar.</summary>
    private async Task<bool> RespondFromCacheAsync(ResourceExecutingContext context, IdempotencyRequestKey key, CancellationToken cancellationToken)
    {
        IdempotencyLookupResult lookup = await LookupAsync(key, cancellationToken).ConfigureAwait(false);
        if (lookup.Outcome == IdempotencyOutcome.HitMatch)
        {
            await ReplayAsync(context, lookup.Entry!, cancellationToken).ConfigureAwait(false);
            return true;
        }

        DomainError? refusal = RefusalFromCache(lookup.Outcome);
        if (refusal is not null)
        {
            Refuse(context, refusal);
        }

        return refusal is not null;
    }

    private static DomainError? RefusalFromCache(IdempotencyOutcome outcome) => outcome switch
    {
        IdempotencyOutcome.HitMismatch => new DomainError(IdempotencyDomainErrorCodes.BodyMismatch, "Mesma Idempotency-Key reusada com body diferente."),
        IdempotencyOutcome.Processing => new DomainError(IdempotencyDomainErrorCodes.ProcessingConflict, "Request com a mesma Idempotency-Key ainda em processamento; tentar novamente."),
        _ => null,
    };

    private async Task ReplayAsync(ResourceExecutingContext context, IdempotencyEntry entry, CancellationToken cancellationToken)
    {
        byte[] encrypted = entry.EncryptedResponseBody
            ?? throw new InvalidOperationException("A entrada de idempotência não guarda o corpo da resposta.");
        byte[] body = await _encryption.DecryptAsync(IdempotencyOptions.EncryptionKeyName, encrypted, cancellationToken).ConfigureAwait(false);

        StoredResponse.Replay(context, entry, body);
    }

    // O atributo vale no método ou na classe; o do método vence, e o da classe cobre "todos os POSTs deste controller".
    private static RequiresIdempotencyKeyAttribute? AttributeOfAction(ResourceExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor descriptor
            ? descriptor.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)
                ?? descriptor.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)
            : null;

    private void Refuse(ResourceExecutingContext context, string code, string detail) =>
        Refuse(context, new DomainError(code, detail));

    private void Refuse(ResourceExecutingContext context, DomainError error) =>
        context.Result = Result.Failure(error).ToActionResult(_errorMapper);
}

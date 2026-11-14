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
    private readonly ExecucaoIdempotente _execucao;

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
        _execucao = new ExecucaoIdempotente(store, encryption);
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        RequiresIdempotencyKeyAttribute? atributo = AtributoDaAction(context);
        if (atributo is null)
        {
            await next().ConfigureAwait(false);
        }
        else
        {
            await ExecutarIdempotenteAsync(context, next, atributo).ConfigureAwait(false);
        }
    }

    private async Task ExecutarIdempotenteAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, RequiresIdempotencyKeyAttribute atributo)
    {
        Result<ChaveDaRequisicao> chave = await LeitorDaChaveDaRequisicao
            .LerAsync(context.HttpContext, _userContext, _options.MaxBodyBytes)
            .ConfigureAwait(false);
        if (chave.IsFailure)
        {
            Recusar(context, chave.Error!);
            return;
        }

        if (await ReservarAsync(context, chave.Value!, atributo).ConfigureAwait(false))
        {
            await _execucao.ExecutarAsync(context, next, chave.Value!).ConfigureAwait(false);
        }
    }

    // Em miss, reserva a chave. Se a UNIQUE perder a corrida, relê: o vencedor pode já ter concluído (replay), ainda estar
    // em Processing (409) ou ter corpo diferente (422). Sem a releitura, o perdedor receberia 409 mesmo com o vencedor
    // pronto para replay, quebrando o contrato do draft IETF §3.
    private async Task<bool> ReservarAsync(ResourceExecutingContext context, ChaveDaRequisicao chave, RequiresIdempotencyKeyAttribute atributo)
    {
        CancellationToken abortado = context.HttpContext.RequestAborted;

        if (await ResponderPeloCacheAsync(context, chave, abortado).ConfigureAwait(false))
        {
            return false;
        }

        if (await TentarReservarAsync(chave, atributo, abortado).ConfigureAwait(false))
        {
            return true;
        }

        await ResponderAposACorridaAsync(context, chave, abortado).ConfigureAwait(false);
        return false;
    }

    private Task<bool> TentarReservarAsync(ChaveDaRequisicao chave, RequiresIdempotencyKeyAttribute atributo, CancellationToken cancellationToken)
    {
        TimeSpan validade = atributo.TtlSeconds >= 0 ? TimeSpan.FromSeconds(atributo.TtlSeconds) : _options.Ttl;
        return _store.TryReserveAsync(chave.Scope, chave.Endpoint, chave.Chave, chave.ResumoDoCorpo, _time.GetUtcNow().Add(validade), cancellationToken);
    }

    private Task<IdempotencyLookupResult> ProcurarAsync(ChaveDaRequisicao chave, CancellationToken cancellationToken) =>
        _store.LookupAsync(chave.Scope, chave.Endpoint, chave.Chave, chave.ResumoDoCorpo, cancellationToken);

    private async Task ResponderAposACorridaAsync(ResourceExecutingContext context, ChaveDaRequisicao chave, CancellationToken cancellationToken)
    {
        if (await ResponderPeloCacheAsync(context, chave, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Miss aqui só ocorre se a entrada foi apagada entre a reserva e a releitura (5xx do vencedor): raro, e o 409 com
        // retry imediato é o caminho seguro.
        Recusar(context, IdempotencyDomainErrorCodes.ProcessingConflict, "Request concorrente com mesma Idempotency-Key resolvida; tentar novamente.");
    }

    /// <summary>Responde a requisição pelo que o cache já sabe; <c>false</c> em miss, quando o handler deve rodar.</summary>
    private async Task<bool> ResponderPeloCacheAsync(ResourceExecutingContext context, ChaveDaRequisicao chave, CancellationToken cancellationToken)
    {
        IdempotencyLookupResult lookup = await ProcurarAsync(chave, cancellationToken).ConfigureAwait(false);
        if (lookup.Outcome == IdempotencyOutcome.HitMatch)
        {
            await ReproduzirAsync(context, lookup.Entry!, cancellationToken).ConfigureAwait(false);
            return true;
        }

        DomainError? recusa = RecusaDoCache(lookup.Outcome);
        if (recusa is not null)
        {
            Recusar(context, recusa);
        }

        return recusa is not null;
    }

    private static DomainError? RecusaDoCache(IdempotencyOutcome outcome) => outcome switch
    {
        IdempotencyOutcome.HitMismatch => new DomainError(IdempotencyDomainErrorCodes.BodyMismatch, "Mesma Idempotency-Key reusada com body diferente."),
        IdempotencyOutcome.Processing => new DomainError(IdempotencyDomainErrorCodes.ProcessingConflict, "Request com a mesma Idempotency-Key ainda em processamento; tentar novamente."),
        _ => null,
    };

    private async Task ReproduzirAsync(ResourceExecutingContext context, IdempotencyEntry entrada, CancellationToken cancellationToken)
    {
        byte[] cifrado = entrada.ResponseBodyCipher
            ?? throw new InvalidOperationException("A entrada de idempotência não guarda o corpo da resposta.");
        byte[] corpo = await _encryption.DecryptAsync(IdempotencyOptions.EncryptionKeyName, cifrado, cancellationToken).ConfigureAwait(false);

        RespostaGuardada.Reproduzir(context, entrada, corpo);
    }

    // O atributo vale no método ou na classe; o do método vence, e o da classe cobre "todos os POSTs deste controller".
    private static RequiresIdempotencyKeyAttribute? AtributoDaAction(ResourceExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor descritor
            ? descritor.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)
                ?? descritor.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)
            : null;

    private void Recusar(ResourceExecutingContext context, string codigo, string detalhe) =>
        Recusar(context, new DomainError(codigo, detalhe));

    private void Recusar(ResourceExecutingContext context, DomainError erro) =>
        context.Result = Result.Failure(erro).ToActionResult(_errorMapper);
}

namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Cryptography;

using Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Executa a action de uma requisição idempotente já reservada, capturando a resposta, e decide o destino da reserva: guarda
/// a resposta cifrada quando ela reflete uma mutação persistida, ou libera a chave para o cliente repetir.
/// </summary>
internal sealed class IdempotentExecution(IIdempotencyStore store, IUniPlusEncryptionService encryption)
{
    public async Task ExecuteAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, IdempotencyRequestKey key)
    {
        ResponseCapture capture = new(context.HttpContext.Response);
        await using (capture.ConfigureAwait(false))
        {
            ResourceExecutedContext executed = await ExecuteOrReleaseAsync(next, key).ConfigureAwait(false);
            await ConcludeAsync(context.HttpContext, executed, key, capture).ConfigureAwait(false);
        }
    }

    // Só a falha de dentro do next() libera a reserva: o handler nem completou, e o retry pode rodar de novo. Depois que ele
    // completou, a mutação pode já estar commitada, e apagar a reserva deixaria o retry recriar o agregado (dois
    // EditalPublicado para o mesmo edital). Ali a entrada fica em Processing até o TTL, e o cliente recebe 409 nesse intervalo
    // (ADR-0027 §"Atomicidade parcial").
    private async Task<ResourceExecutedContext> ExecuteOrReleaseAsync(ResourceExecutionDelegate next, IdempotencyRequestKey key)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch
        {
            await ReleaseWithoutFailingAsync(key).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ConcludeAsync(HttpContext http, ResourceExecutedContext executed, IdempotencyRequestKey key, ResponseCapture capture)
    {
        // O ResourceInvoker não relança do next(): ele devolve o contexto com a exceção e só a relança depois que todos os
        // resource filters retornam sem marcá-la como tratada. A reserva não é apagada: next() cobre também a serialização
        // do resultado, e a exceção pode ter nascido depois de o comando persistir.
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await ReleaseIfFailureBeforeMutationAsync(executed.Exception, key).ConfigureAwait(false);
            return;
        }

        await (ShouldReleaseReservation(executed, http.Response.StatusCode) ? ReleaseAsync(key) : StoreAsync(http, key, capture)).ConfigureAwait(false);
        await capture.ReturnToClientAsync(http.RequestAborted).ConfigureAwait(false);
    }

    // Só se guarda a resposta que reflete uma mutação persistida (ADR-0027 §"Status codes em cache"): o 2xx, e o 4xx que o
    // handler devolve depois de gravar (RespostaDeRecusaAposGravacao), pois um retry o executaria sobre o estado já mudado.
    // Todo outro 4xx produzido dentro do MVC, e todo 5xx, libera a reserva: o 4xx recusa a requisição, não a executa, e
    // guardá-lo prenderia por 24 h o cliente que corrige a causa e repete a chave.
    private static bool ShouldReleaseReservation(ResourceExecutedContext executed, int status)
    {
        bool refusalAfterWrite = status < 500 && executed.Result is RespostaDeRecusaAposGravacao;
        return (status >= 400 && !refusalAfterWrite) || executed.Canceled;
    }

    private async Task StoreAsync(HttpContext http, IdempotencyRequestKey key, ResponseCapture capture)
    {
        byte[] encrypted = await encryption
            .EncryptAsync(IdempotencyOptions.EncryptionKeyName, capture.Content(), http.RequestAborted)
            .ConfigureAwait(false);

        await store.CompleteAsync(
            key.Scope, key.Endpoint, key.IdempotencyKey, http.Response.StatusCode,
            StoredResponse.SerializeHeaders(http.Response), encrypted, http.RequestAborted).ConfigureAwait(false);
    }

    // A falha de registrar o acesso e o catálogo desatualizado nascem antes da mutação: nada foi persistido, e prender a chave
    // por 24 h faria a repetição, que agora poderia dar certo, receber 409.
    private async Task ReleaseIfFailureBeforeMutationAsync(Exception failure, IdempotencyRequestKey key)
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is RegistroDeAcessoNaoGravadoException or CatalogoDesatualizadoException)
            {
                await ReleaseAsync(key).ConfigureAwait(false);
                return;
            }
        }
    }

    // CancellationToken.None: o RequestAborted já está disparado quando o cliente cancelou a requisição.
    private Task ReleaseAsync(IdempotencyRequestKey key) =>
        store.DeleteAsync(key.Scope, key.Endpoint, key.IdempotencyKey, CancellationToken.None);

    private async Task ReleaseWithoutFailingAsync(IdempotencyRequestKey key)
    {
        try
        {
            await ReleaseAsync(key).ConfigureAwait(false);
        }
        catch
        {
            // Melhor esforço: se o delete falhar, a entrada expira pelo TTL.
        }
    }
}

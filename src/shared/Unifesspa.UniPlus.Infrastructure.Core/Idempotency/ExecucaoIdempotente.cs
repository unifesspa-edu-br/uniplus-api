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
internal sealed class ExecucaoIdempotente(IIdempotencyStore store, IUniPlusEncryptionService encryption)
{
    public async Task ExecutarAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, ChaveDaRequisicao chave)
    {
        CapturaDaResposta captura = new(context.HttpContext.Response);
        await using (captura.ConfigureAwait(false))
        {
            ResourceExecutedContext executado = await ExecutarOuLiberarAsync(next, chave).ConfigureAwait(false);
            await ConcluirAsync(context.HttpContext, executado, chave, captura).ConfigureAwait(false);
        }
    }

    // Só a falha de dentro do next() libera a reserva: o handler nem completou, e o retry pode rodar de novo. Depois que ele
    // completou, a mutação pode já estar commitada, e apagar a reserva deixaria o retry recriar o agregado (dois
    // EditalPublicado para o mesmo edital). Ali a entrada fica em Processing até o TTL, e o cliente recebe 409 nesse intervalo
    // (ADR-0027 §"Atomicidade parcial").
    private async Task<ResourceExecutedContext> ExecutarOuLiberarAsync(ResourceExecutionDelegate next, ChaveDaRequisicao chave)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch
        {
            await LiberarSemFalharAsync(chave).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ConcluirAsync(HttpContext http, ResourceExecutedContext executado, ChaveDaRequisicao chave, CapturaDaResposta captura)
    {
        // O ResourceInvoker não relança do next(): ele devolve o contexto com a exceção e só a relança depois que todos os
        // resource filters retornam sem marcá-la como tratada. A reserva não é apagada: next() cobre também a serialização
        // do resultado, e a exceção pode ter nascido depois de o comando persistir.
        if (executado.Exception is not null && !executado.ExceptionHandled)
        {
            await LiberarSeAFalhaNasceuAntesDaMutacaoAsync(executado.Exception, chave).ConfigureAwait(false);
            return;
        }

        await (DeveLiberarAReserva(executado, http.Response.StatusCode) ? LiberarAsync(chave) : GuardarAsync(http, chave, captura)).ConfigureAwait(false);
        await captura.DevolverAoClienteAsync(http.RequestAborted).ConfigureAwait(false);
    }

    // Só se guarda a resposta que reflete uma mutação persistida (ADR-0027 §"Status codes em cache"): o 2xx, e o 4xx que o
    // handler devolve depois de gravar (RespostaDeRecusaAposGravacao), pois um retry o executaria sobre o estado já mudado.
    // Todo outro 4xx produzido dentro do MVC, e todo 5xx, libera a reserva: o 4xx recusa a requisição, não a executa, e
    // guardá-lo prenderia por 24 h o cliente que corrige a causa e repete a chave.
    private static bool DeveLiberarAReserva(ResourceExecutedContext executado, int status)
    {
        bool recusaAposGravacao = status < 500 && executado.Result is RespostaDeRecusaAposGravacao;
        return (status >= 400 && !recusaAposGravacao) || executado.Canceled;
    }

    private async Task GuardarAsync(HttpContext http, ChaveDaRequisicao chave, CapturaDaResposta captura)
    {
        byte[] cifrado = await encryption
            .EncryptAsync(IdempotencyOptions.EncryptionKeyName, captura.Conteudo(), http.RequestAborted)
            .ConfigureAwait(false);

        await store.CompleteAsync(
            chave.Scope, chave.Endpoint, chave.Chave, http.Response.StatusCode,
            RespostaGuardada.SerializarCabecalhos(http.Response), cifrado, http.RequestAborted).ConfigureAwait(false);
    }

    // A falha de registrar o acesso e o catálogo desatualizado nascem antes da mutação: nada foi persistido, e prender a chave
    // por 24 h faria a repetição, que agora poderia dar certo, receber 409.
    private async Task LiberarSeAFalhaNasceuAntesDaMutacaoAsync(Exception falha, ChaveDaRequisicao chave)
    {
        for (Exception? atual = falha; atual is not null; atual = atual.InnerException)
        {
            if (atual is RegistroDeAcessoNaoGravadoException or CatalogoDesatualizadoException)
            {
                await LiberarAsync(chave).ConfigureAwait(false);
                return;
            }
        }
    }

    // CancellationToken.None: o RequestAborted já está disparado quando o cliente cancelou a requisição.
    private Task LiberarAsync(ChaveDaRequisicao chave) =>
        store.DeleteAsync(chave.Scope, chave.Endpoint, chave.Chave, CancellationToken.None);

    private async Task LiberarSemFalharAsync(ChaveDaRequisicao chave)
    {
        try
        {
            await LiberarAsync(chave).ConfigureAwait(false);
        }
        catch
        {
            // Melhor esforço: se o delete falhar, a entrada expira pelo TTL.
        }
    }
}

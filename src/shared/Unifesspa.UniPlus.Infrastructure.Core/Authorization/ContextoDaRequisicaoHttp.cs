namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Monta o contexto da requisição que acompanha toda decisão de acesso.</summary>
internal static class ContextoDaRequisicaoHttp
{
    // A correlação é a que o middleware propagou (X-Correlation-Id, quando a
    // requisição participa de um fluxo entre serviços), e não o identificador
    // local do ASP.NET: usá-lo daria ao registro da decisão um valor diferente
    // do que sai nos logs da aplicação e nas mensagens seguintes, justamente
    // impedindo a correlação que o campo existe para permitir.
    //
    // O instante do acesso é lido uma vez e acompanha a decisão inteira, de
    // modo que validade de concessão e registro falem do mesmo momento.
    public static Result<AuthorizationRequestContext> Montar(
        HttpContext? http,
        ICorrelationIdAccessor correlationId,
        TimeProvider relogio)
        => AuthorizationRequestContext.From(
            correlationId.CorrelationId
                ?? http?.TraceIdentifier
                ?? Guid.CreateVersion7().ToString(),
            relogio.GetUtcNow(),
            OrigemRequisicao.Api,
            ipOrigem: http?.Connection.RemoteIpAddress?.ToString(),
            userAgent: http?.Request.Headers.UserAgent.ToString(),
            execucaoId: ExecucaoDaRequisicao(http));

    // Um identificador por requisição HTTP, gerado aqui: o de correlação vem do cliente e não serve de chave. Todas as
    // decisões da mesma requisição o compartilham, e é isso que faz a nova tentativa de um acesso não duplicá-lo.
    private static Guid? ExecucaoDaRequisicao(HttpContext? http)
    {
        if (http is null)
        {
            return null;
        }

        if (http.Items.TryGetValue(ChaveDaExecucao, out object? existente) && existente is Guid id)
        {
            return id;
        }

        Guid novo = Guid.CreateVersion7();
        http.Items[ChaveDaExecucao] = novo;
        return novo;
    }

    private const string ChaveDaExecucao = "uniplus.autorizacao.execucao-id";
}

namespace Unifesspa.UniPlus.Selecao.API.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Decide a leitura do processo da rota (<c>{id}</c>): o processo que saiu de rascunho é lido por qualquer pessoa, e o
/// rascunho só por quem configura na unidade dona (ADR-0078, ADR-0151). Roda antes do <i>model binding</i>.
/// </summary>
/// <remarks>
/// A recusa é sempre o <c>404</c> do processo inexistente, também para quem não se identificou: o endpoint é anônimo, e
/// um <c>401</c> diante de um rascunho revelaria que ele existe. O <c>401</c> fica só para o token autenticado cuja
/// identidade está incompleta.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigeLeituraDoProcessoAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceProvider servicos = context.HttpContext.RequestServices;
        Guid processoId = RotaDoProcesso.Id(context);

        ResultadoDoAcesso resultado = await AcessoAoProcesso
            .VerificarLeituraAsync(
                servicos.GetRequiredService<IVerificadorDeAcesso>(),
                servicos.GetRequiredService<IProcessoSeletivoRepository>(),
                processoId,
                context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        context.Result = resultado switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta when context.HttpContext.User.Identity?.IsAuthenticated == true =>
                new ChallengeResult(),
            _ => RotaDoProcesso.NaoEncontrado(servicos, processoId),
        };
    }
}

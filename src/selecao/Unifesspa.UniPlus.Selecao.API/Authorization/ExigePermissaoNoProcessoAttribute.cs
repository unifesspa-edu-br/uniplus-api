namespace Unifesspa.UniPlus.Selecao.API.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Exige uma permissão da Seleção sobre o processo da rota (<c>{id}</c>), na unidade que o possui (ADR-0078, ADR-0151).
/// Roda como filtro de autorização, antes do <i>model binding</i>: uma recusa de acesso não pode depender de o corpo ser
/// válido, nem a resposta de um corpo inválido revelar que o processo existe.
/// </summary>
/// <remarks>
/// Negado e inexistente respondem o mesmo <c>404</c> do recurso, a identidade incompleta responde <c>401</c> e o
/// impedimento por inscrição no processo responde <c>403</c>: quem tem a concessão já sabe que o processo existe. O filtro
/// do catálogo (<c>ExigePermissao</c>) não serve a estas permissões porque não conhece a unidade dona do processo.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigePermissaoNoProcessoAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly PermissaoSelecao _permissao;

    /// <summary>Cria a exigência.</summary>
    /// <param name="permissao">Permissão exigida na unidade dona do processo.</param>
    public ExigePermissaoNoProcessoAttribute(PermissaoSelecao permissao) => _permissao = permissao;

    /// <summary>Tipo do recurso que a decisão enxerga. Omitido, é o processo seletivo.</summary>
    public string RecursoTipo { get; init; } = AcessoAoProcesso.RecursoProcesso;

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceProvider servicos = context.HttpContext.RequestServices;
        Guid processoId = RotaDoProcesso.Id(context);

        ResultadoDoAcesso resultado = await AcessoAoProcesso
            .VerificarAsync(
                servicos.GetRequiredService<IVerificadorDeAcesso>(),
                servicos.GetRequiredService<IProcessoSeletivoRepository>(),
                processoId,
                _permissao,
                RecursoTipo,
                sensibilidadeDoRecurso: null,
                context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        // Challenge, e não Unauthorized: o 401 canônico carrega WWW-Authenticate e o corpo problem+json escritos
        // pelo OnChallenge do esquema configurado.
        context.Result = resultado switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta => new ChallengeResult(),
            ResultadoDoAcesso.Impedido => RotaDoProcesso.Impedido(servicos),
            _ => RotaDoProcesso.NaoEncontrado(servicos, processoId),
        };
    }
}

namespace Unifesspa.UniPlus.Selecao.API.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Exige uma permissão da Seleção sobre o processo da rota (<c>{id}</c>, ou o parâmetro de <see cref="ParametroDaRota"/>), na unidade que o possui (ADR-0078, ADR-0151).
/// Roda como filtro de autorização, antes do <i>model binding</i>: uma recusa de acesso não pode depender de o corpo ser
/// válido, nem a resposta de um corpo inválido revelar que o processo existe.
/// </summary>
/// <remarks>
/// Negado e inexistente respondem o mesmo <c>404</c> do recurso, a identidade incompleta responde <c>401</c> e o
/// impedimento por inscrição no processo responde <c>403</c>: quem tem a concessão já sabe que o processo existe. O filtro
/// do catálogo (<c>ExigePermissao</c>) não serve a estas permissões porque não conhece a unidade dona do processo.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigePermissaoNoProcessoAttribute : Attribute, IAsyncAuthorizationFilter, IExigeAutorizacaoAlemDoLogin
{
    private readonly PermissaoSelecao _permissao;

    /// <summary>Cria a exigência.</summary>
    /// <param name="permissao">Permissão exigida na unidade dona do processo.</param>
    public ExigePermissaoNoProcessoAttribute(PermissaoSelecao permissao) => _permissao = permissao;

    /// <summary>
    /// O alvo da decisão. Omitido, é o processo seletivo; as inscrições do processo guardam dado pessoal, então a decisão
    /// sobre elas entra no registro de acessos ainda que a permissão de acesso seja interna.
    /// </summary>
    public AlvoDaPermissaoNoProcesso Alvo { get; init; } = AlvoDaPermissaoNoProcesso.Processo;

    /// <summary>Nome do parâmetro de rota que traz o identificador do processo. Omitido, é <c>id</c>.</summary>
    public string ParametroDaRota { get; init; } = RotaDoProcesso.ParametroPadrao;

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceProvider servicos = context.HttpContext.RequestServices;
        Guid processoId = RotaDoProcesso.Id(context, ParametroDaRota);

        ResultadoDoAcesso resultado = await AcessoAoProcesso
            .VerificarAsync(
                servicos.GetRequiredService<IVerificadorDeAcesso>(),
                servicos.GetRequiredService<IProcessoSeletivoRepository>(),
                processoId,
                _permissao,
                unidade => Alvo == AlvoDaPermissaoNoProcesso.Inscricoes
                    ? AcessoAoProcesso.ResourceDaInscricao(unidade, processoId, inscricaoId: null)
                    : AcessoAoProcesso.ResourceDoProcesso(unidade, processoId),
                context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        context.Result = RespostaDoAcesso.Para(
            resultado, () => RotaDoProcesso.NaoEncontrado(servicos, processoId), () => RotaDoProcesso.Impedido(servicos));
    }
}

/// <summary>O alvo que <see cref="ExigePermissaoNoProcessoAttribute"/> decide sobre o processo da rota.</summary>
public enum AlvoDaPermissaoNoProcesso
{
    /// <summary>O próprio processo seletivo.</summary>
    Processo = 0,

    /// <summary>Todas as inscrições do processo.</summary>
    Inscricoes = 1,
}

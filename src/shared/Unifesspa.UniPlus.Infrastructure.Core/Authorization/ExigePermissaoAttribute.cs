namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Exige uma permissão do catálogo antes do <i>model binding</i> (ADR-0151). Preserva
/// os três desfechos da borda: permitido segue; negado responde <c>403</c>; identidade
/// incompleta responde <c>401</c>.
/// </summary>
/// <typeparam name="TPermissao">Enum de permissões do módulo.</typeparam>
/// <remarks>
/// Roda como filtro de autorização, que precede o <i>binding</i> — um filtro de ação
/// só roda depois dele, e uma rejeição de acesso não pode depender de o corpo ser
/// válido. O C# não admite parâmetro de atributo do tipo <typeparamref name="TPermissao"/>
/// (CS0181), então o construtor recebe o valor como <see cref="Enum"/>; o tipo é
/// conferido ao construir o atributo, e a conferência de todos os usos é de um teste.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigePermissaoAttribute<TPermissao> : Attribute, IAsyncAuthorizationFilter
    where TPermissao : struct, Enum
{
    private readonly ResourceContext? _recurso;

    /// <summary>Cria a exigência.</summary>
    /// <param name="permissao">Permissão exigida, do enum <typeparamref name="TPermissao"/>.</param>
    public ExigePermissaoAttribute(object permissao)
    {
        if (permissao is not TPermissao tipada)
        {
            throw new ArgumentException(
                $"A permissão deve ser um {typeof(TPermissao).Name}, e não {permissao?.GetType().Name ?? "nulo"}.",
                nameof(permissao));
        }

        Definicao = CatalogoDePermissoes<TPermissao>.Obter(tipada);
    }

    /// <summary>
    /// Tipo do recurso que a decisão enxerga, quando a concessão pode ser restrita
    /// por tipo. Omitido, o alvo é a própria operação.
    /// </summary>
    public string? RecursoTipo
    {
        get => _recurso?.RecursoTipo;
        init => _recurso = value is null
            ? null
            : ResourceContext.From(value, Definicao.Requisito.Sensibilidade).Value
                ?? throw new ArgumentException("O tipo do recurso não pode ser vazio.", nameof(value));
    }

    /// <summary>Definição da permissão exigida.</summary>
    internal DefinicaoDePermissao Definicao { get; }

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IVerificadorDeAcesso acesso = context.HttpContext.RequestServices.GetRequiredService<IVerificadorDeAcesso>();

        ResultadoDoAcesso resultado = await acesso
            .VerificarAsync(Definicao.Requisito, _recurso, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        // Challenge, e não Unauthorized: o 401 canônico carrega WWW-Authenticate e o
        // corpo problem+json escritos pelo OnChallenge do esquema configurado.
        context.Result = resultado switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta => new ChallengeResult(),
            _ => new ForbidResult(),
        };
    }
}

namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Sinaliza que um parâmetro <see cref="PageRequest"/> da action deve ser
/// preenchido pelo <see cref="PageRequestModelBinder"/>: lê <c>cursor</c> e
/// <c>limit</c> do query string, decoda o cursor opaco (ADR-0026), valida
/// o <see cref="Resource"/> contra o <c>ResourceTag</c> embutido no payload e
/// publica o <see cref="PageRequest"/> resultante para a action consumir.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class FromCursorAttribute : ModelBinderAttribute
{
    /// <param name="resource">Identificador do recurso embutido no payload do cursor.</param>
    /// <param name="scopeRouteValues">Nomes dos valores de rota que escopam a coleção; vazio quando ela é única.</param>
    public FromCursorAttribute(string resource, params string[] scopeRouteValues)
        : base(typeof(PageRequestModelBinder))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentNullException.ThrowIfNull(scopeRouteValues);
        Resource = resource;
        ScopeRouteValues = Array.AsReadOnly([.. scopeRouteValues]);
        BindingSource = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Query;
    }

    /// <summary>Identificador do recurso embutido no payload do cursor (ex.: <c>"editais"</c>).</summary>
    public string Resource { get; }

    /// <summary>
    /// Quando <c>true</c>, o cursor leva <see cref="CursorPayload.ResumoDaConta"/>, o resumo do par emissor e
    /// conta de quem o recebeu, e o binder confere o mesmo resumo na volta — cursor de Alice não é navegável por Bob
    /// mesmo que decifre corretamente (gap LGPD em metadata de recursos user-scoped, ADR-0026). Default
    /// <c>false</c> (recurso público).
    /// </summary>
    public bool RequireUserBinding { get; init; }

    /// <summary>
    /// Nomes dos valores de rota que escopam a coleção (ex.:
    /// <c>["entidadeTipo", "entidadeId"]</c> em
    /// <c>/api/publicacoes/entidades/{entidadeTipo}/{entidadeId}/atos</c>), declarados depois do recurso:
    /// <c>[FromCursor("atos", "entidadeTipo", "entidadeId")]</c>. A etiqueta do cursor é composta com eles
    /// (<see cref="CursorResourceTag"/>), de modo que um cursor emitido para uma entidade não seja navegável
    /// na coleção de outra — mesmo recurso, coleções distintas. Vazio na maioria dos recursos, cuja coleção
    /// é única e a etiqueta, uma constante.
    /// </summary>
    /// <remarks>
    /// Emissão e conferência compõem a etiqueta pelo mesmo <see cref="ResourceTagFor"/>, com os mesmos valores
    /// na mesma ordem: o escritor da resposta e o binder leem esta declaração, e nenhum cursor emitido deixa de
    /// passar na conferência.
    /// </remarks>
    public IReadOnlyList<string> ScopeRouteValues { get; }

    /// <summary>
    /// Acha a declaração do cursor da ação: olha a <see cref="ParameterInfo"/> do
    /// <see cref="ControllerParameterDescriptor"/>, porque o <c>BindingInfo</c> expõe o
    /// <c>BinderType</c>, mas não o <see cref="FromCursorAttribute"/>. Assim quem lê fica resiliente a
    /// renomes do parâmetro. Devolve <c>null</c> quando a ação não é de controller ou não declara o cursor.
    /// </summary>
    internal static FromCursorAttribute? Of(ActionDescriptor descriptor)
    {
        if (descriptor is not ControllerActionDescriptor controllerAction)
        {
            return null;
        }

        return controllerAction.Parameters
            .OfType<ControllerParameterDescriptor>()
            .Where(parameter => parameter.ParameterType == typeof(PageRequest))
            .Select(parameter => parameter.ParameterInfo.GetCustomAttribute<FromCursorAttribute>())
            .FirstOrDefault(attribute => attribute is not null);
    }

    /// <summary>
    /// A etiqueta do cursor inclui os valores de rota que escopam a coleção (ADR-0026): sem eles, um cursor
    /// emitido na coleção de uma entidade retomaria a paginação na coleção de outra, num ponto de continuação que não é dele.
    /// </summary>
    internal string ResourceTagFor(RouteData routeData) => CursorResourceTag.Compose(
        Resource,
        ScopeRouteValues.Select(name => ReadRouteValue(routeData, name)));

    private static string? ReadRouteValue(RouteData routeData, string name) =>
        routeData.Values.TryGetValue(name, out object? value) ? value?.ToString() : null;
}

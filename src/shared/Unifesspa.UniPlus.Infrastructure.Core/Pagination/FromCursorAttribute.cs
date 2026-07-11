namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    public FromCursorAttribute(string resource)
        : base(typeof(PageRequestModelBinder))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        Resource = resource;
        BindingSource = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Query;
    }

    /// <summary>Identificador do recurso embutido no payload do cursor (ex.: <c>"editais"</c>).</summary>
    public string Resource { get; }

    /// <summary>
    /// Quando <c>true</c>, o binder popula <see cref="CursorPayload.UserId"/>
    /// com o sub claim do principal autenticado na emissão do cursor e valida
    /// igualdade no decode — cursor de Alice não é navegável por Bob mesmo
    /// que decoda corretamente (gap LGPD em metadata de recursos user-scoped,
    /// ADR-0026). Default <c>false</c> (recurso público).
    /// </summary>
    public bool RequireUserBinding { get; init; }

    /// <summary>
    /// Quando <c>true</c>, cursores de continuação precisam trazer
    /// <see cref="CursorPayload.SortKey"/>. Use em recursos com keyset ordenado
    /// multi-coluna, nos quais a âncora é o par <c>(SortKey, Id)</c>.
    /// </summary>
    public bool RequireSortKey { get; init; }

    /// <summary>
    /// Nomes dos valores de rota que escopam a coleção (ex.:
    /// <c>["entidadeTipo", "entidadeId"]</c> em
    /// <c>/api/publicacoes/entidades/{entidadeTipo}/{entidadeId}/atos</c>). O binder
    /// compõe a etiqueta esperada com eles (<see cref="CursorResourceTag"/>), de modo que
    /// um cursor emitido para uma entidade não seja navegável na coleção de outra — mesmo
    /// recurso, coleções distintas. Vazio na maioria dos recursos, cuja coleção é única e
    /// a etiqueta, uma constante.
    /// </summary>
    /// <remarks>
    /// Quem emite tem de compor a etiqueta com os <b>mesmos</b> valores, na mesma ordem,
    /// pelo mesmo <see cref="CursorResourceTag.Compose"/> — senão nenhum cursor emitido
    /// passa na conferência.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Argumento nomeado de atributo — a linguagem só admite array aqui.")]
    public string[] ScopeRouteValues { get; init; } = [];
}

namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

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
}

namespace Unifesspa.UniPlus.Selecao.API.Http;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Tira da action os provedores de valor de formulário. Sem isto o MVC lê o formulário inteiro antes
/// de a action começar, e um corpo acima do limite do servidor estoura nessa leitura, fora do alcance
/// da action — que não consegue então responder com a recusa de tamanho que o contrato promete.
/// A action que usa este atributo lê o formulário ela mesma, depois de conferir o tamanho.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DesligaOVinculoDoFormularioAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IList<IValueProviderFactory> fabricas = context.ValueProviderFactories;
        for (int i = fabricas.Count - 1; i >= 0; i--)
        {
            if (fabricas[i] is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory)
            {
                fabricas.RemoveAt(i);
            }
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

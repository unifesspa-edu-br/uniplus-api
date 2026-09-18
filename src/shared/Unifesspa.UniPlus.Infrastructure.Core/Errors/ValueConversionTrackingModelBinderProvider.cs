namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Envolve os binders do MVC para anotar qual chave falhou <b>na conversão</b>, distinguindo-a
/// da que reprovou numa validação depois do binding.
/// </summary>
/// <remarks>
/// <para>
/// Envolve todos os providers, e não uma lista nominal dos que tratam tipo simples: a lista
/// nominal envelhece calada — um tipo novo ganha binder próprio, ninguém lembra de acrescentá-lo
/// aqui, e a recusa dele volta ao envelope do framework sem nada acusar. O recorte de quem
/// interessa não é feito por tipo de provider, e sim pela origem do valor, dentro do binder.
/// </para>
/// <para>
/// O corpo fica de fora: a recusa de leitura dele já tem caminho próprio, que sabe distinguir
/// documento ilegível de campo obrigatório ausente e de corpo inexistente — três causas que o
/// cliente corrige de formas diferentes. Anotar o corpo aqui achataria as três numa só.
/// </para>
/// </remarks>
public sealed class ValueConversionTrackingModelBinderProvider : IModelBinderProvider
{
    private readonly IModelBinderProvider _inner;

    public ValueConversionTrackingModelBinderProvider(IModelBinderProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        IModelBinder? binder = _inner.GetBinder(context);

        return binder is null ? null : new ValueConversionTrackingModelBinder(binder);
    }

    private sealed class ValueConversionTrackingModelBinder(IModelBinder inner) : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            string chave = bindingContext.ModelName;
            int antes = Erros(bindingContext.ModelState, chave);

            await inner.BindModelAsync(bindingContext).ConfigureAwait(false);

            // Erro que apareceu AGORA, sob a chave deste parâmetro, foi o binder que o pôs —
            // e o binder só reprova por uma razão: o valor não virou o tipo. A validação de
            // atributo roda depois que todo o binding terminou, e por isso nunca cai aqui.
            if (Erros(bindingContext.ModelState, chave) > antes
                && bindingContext.BindingSource != BindingSource.Body)
            {
                ValueConversionFailures.Record(bindingContext.HttpContext, chave);
            }
        }

        private static int Erros(ModelStateDictionary modelState, string chave) =>
            modelState.TryGetValue(chave, out ModelStateEntry? entrada) ? entrada.Errors.Count : 0;
    }
}

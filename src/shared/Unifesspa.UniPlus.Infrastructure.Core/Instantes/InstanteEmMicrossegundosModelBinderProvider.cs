namespace Unifesspa.UniPlus.Infrastructure.Core.Instantes;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Trunca em microssegundos o instante que chega na rota, na query ou no cabeçalho. A conversão
/// é a do binder simples do MVC, com a cultura do provedor de valores e o erro no
/// <c>ModelState</c> para o valor inválido; só o resultado bem-sucedido é truncado.
/// </summary>
public sealed class InstanteEmMicrossegundosModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.UnderlyingOrModelType != typeof(DateTimeOffset))
        {
            return null;
        }

        ILoggerFactory loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();

        return new InstanteTruncado(new SimpleTypeModelBinder(context.Metadata.ModelType, loggerFactory));
    }

    private sealed class InstanteTruncado(IModelBinder simples) : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            await simples.BindModelAsync(bindingContext).ConfigureAwait(false);

            if (bindingContext.Result is { IsModelSet: true, Model: DateTimeOffset instante })
            {
                bindingContext.Result = ModelBindingResult.Success(instante.TruncadoEmMicrossegundos());
            }
        }
    }
}

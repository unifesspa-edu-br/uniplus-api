namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Formatters;

/// <summary>
/// Faz o body model binder converter também recusas de desserialização fora de
/// <see cref="InputFormatterException"/> em erros de model state.
/// </summary>
internal sealed class PolymorphicDeserializationInputFormatter(IInputFormatter inner) :
    IInputFormatter,
    IInputFormatterExceptionPolicy
{
    public InputFormatterExceptionPolicy ExceptionPolicy =>
        InputFormatterExceptionPolicy.MalformedInputExceptions;

    public bool CanRead(InputFormatterContext context) => inner.CanRead(context);

    public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        try
        {
            return await inner.ReadAsync(context).ConfigureAwait(false);
        }
        catch (NotSupportedException exception) when (EhDiscriminatorAusente(context, exception))
        {
            // O body model binder só preserva a posição do erro quando ela entra
            // no ModelState. Isso cobre unions polimórficas sem discriminator, que
            // o System.Text.Json reporta como NotSupportedException.
            context.ModelState.TryAddModelException("$", exception);
            RegistrarFalha(context);
            return InputFormatterResult.Failure();
        }
    }

    private static bool EhDiscriminatorAusente(
        InputFormatterContext context,
        NotSupportedException exception) =>
        exception.Message.Contains("discriminator", StringComparison.OrdinalIgnoreCase)
        && ObterPropriedadePolimorfica(context.ModelType) is not null;

    private static void RegistrarFalha(InputFormatterContext context)
    {
        if (ObterPropriedadePolimorfica(context.ModelType) is not { } propriedade)
        {
            return;
        }

        string nomeJson = JsonNamingPolicy.CamelCase.ConvertName(propriedade.Name);
        PolymorphicDeserializationFailures.Record(context.HttpContext, nomeJson, "$tipo");
    }

    private static PropertyInfo? ObterPropriedadePolimorfica(Type tipo) =>
        tipo.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(static propriedade =>
            {
                Type tipoPropriedade = Nullable.GetUnderlyingType(propriedade.PropertyType)
                    ?? propriedade.PropertyType;
                return tipoPropriedade.GetCustomAttribute<JsonPolymorphicAttribute>() is not null;
            });
}

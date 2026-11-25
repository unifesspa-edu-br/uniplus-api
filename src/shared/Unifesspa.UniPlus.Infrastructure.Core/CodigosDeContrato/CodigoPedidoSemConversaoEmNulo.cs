namespace Unifesspa.UniPlus.Infrastructure.Core.CodigosDeContrato;

using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

internal sealed class CodigoPedidoSemConversaoEmNulo : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Attributes.OfType<IAceitaCodigosDe>().Any())
        {
            context.DisplayMetadata.ConvertEmptyStringToNull = false;
        }
    }
}

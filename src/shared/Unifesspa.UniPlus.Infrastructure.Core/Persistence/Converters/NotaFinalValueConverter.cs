namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia NotaFinal ↔ decimal. Precisão (9,4) é definida via convenção no
// ModelBuilder — escala 4 acompanha o arredondamento já aplicado pelo VO
// em NotaFinal.Criar (Math.Round(valor, 4)).
public sealed class NotaFinalValueConverter : ValueConverter<NotaFinal, decimal>
{
    public NotaFinalValueConverter()
        : base(
            nota => nota.Valor,
            valor => ValueObjectMaterialization.Reidratar(NotaFinal.Criar(valor), nameof(NotaFinal)))
    {
    }
}

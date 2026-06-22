namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia Percentual ↔ decimal. A precisão (5,2) é definida na própria
// propriedade via HasPrecision no IEntityTypeConfiguration. A reidratação
// passa por ValueObjectMaterialization para falhar explicitamente (com
// contexto) caso a coluna seja corrompida fora do fluxo da aplicação, em vez
// do NullReferenceException tardio de `Percentual.Criar(v).Value!`.
public sealed class PercentualValueConverter : ValueConverter<Percentual, decimal>
{
    public PercentualValueConverter()
        : base(
            percentual => percentual.Valor,
            valor => ValueObjectMaterialization.Reidratar(Percentual.Criar(valor), nameof(Percentual)))
    {
    }
}

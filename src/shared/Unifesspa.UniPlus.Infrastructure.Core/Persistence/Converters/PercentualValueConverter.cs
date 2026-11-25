namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// A leitura falha com contexto se o valor gravado não respeita a regra do domínio.
public sealed class PercentualValueConverter : ValueConverter<Percentual, decimal>
{
    public PercentualValueConverter()
        : base(
            percentual => percentual.Valor,
            valor => ObjetoDeValorGravado.Reidratar(Percentual.Criar(valor), nameof(Percentual)))
    {
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChaveLocalValueConverter : ValueConverter<ChaveLocal, string>
{
    public ChaveLocalValueConverter()
        : base(
            chave => chave.Valor,
            valor => ObjetoDeValorGravado.Reidratar(ChaveLocal.Criar(valor), nameof(ChaveLocal)))
    {
    }
}

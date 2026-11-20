namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Grava o enum pelo código que o membro declara em <see cref="CodigoDeContratoAttribute"/> e o lê de volta pelo mesmo
/// código.
/// </summary>
/// <typeparam name="TEnum">Enum em que algum membro declara o código.</typeparam>
/// <remarks>
/// A coluna opcional, de <c>TEnum?</c>, usa este mesmo conversor: o EF Core não o chama para nulo, e a coluna
/// <c>NULL</c> continua sendo ausência. Um código que o enum não declara é defeito do dado: a leitura lança
/// <see cref="ArgumentException"/>, sem valor substituto. O conversor não define tamanho nem tipo de coluna.
/// </remarks>
public sealed class CodigoDeContratoValueConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>Cria o conversor do enum.</summary>
    public CodigoDeContratoValueConverter()
        : base(
            valor => valor.Codigo(),
            codigo => CatalogoDe<TEnum>.Obter(codigo))
    {
    }
}

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

// A leitura falha com contexto se o valor gravado não respeita a regra do domínio.
public sealed class CodigoFaseValueConverter : ValueConverter<CodigoFase, string>
{
    public CodigoFaseValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => ObjetoDeValorGravado.Reidratar(CodigoFase.Criar(valor), nameof(CodigoFase)))
    {
    }
}

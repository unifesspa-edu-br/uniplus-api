namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

// A leitura falha com contexto se o valor gravado não respeita a regra do domínio.
public sealed class CodigoDaBaseLegalDoFadaValueConverter : ValueConverter<CodigoDaBaseLegalDoFada, string>
{
    public CodigoDaBaseLegalDoFadaValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => ObjetoDeValorGravado.Reidratar(CodigoDaBaseLegalDoFada.Criar(valor), nameof(CodigoDaBaseLegalDoFada)))
    {
    }
}

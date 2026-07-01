namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoFase ↔ string (varchar). O comprimento da coluna é definido na
// própria propriedade via HasMaxLength no IEntityTypeConfiguration. A reidratação
// falha explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo
// da aplicação, em vez do NullReferenceException tardio de
// `CodigoFase.Criar(v).Value!`.
public sealed class CodigoFaseValueConverter : ValueConverter<CodigoFase, string>
{
    public CodigoFaseValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoFase Reidratar(string valor)
    {
        Result<CodigoFase> resultado = CodigoFase.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoFase)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}

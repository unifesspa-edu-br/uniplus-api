namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoBaseLegalBonusRegional ↔ string (varchar). A reidratação falha com contexto se a coluna foi alterada fora do
// fluxo da aplicação.
public sealed class CodigoBaseLegalBonusRegionalValueConverter : ValueConverter<CodigoBaseLegalBonusRegional, string>
{
    public CodigoBaseLegalBonusRegionalValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoBaseLegalBonusRegional Reidratar(string valor)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoBaseLegalBonusRegional)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}

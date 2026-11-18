namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoTermoConsentimento ↔ string (varchar). A reidratação falha com contexto se a coluna foi alterada fora do
// fluxo da aplicação.
public sealed class CodigoTermoConsentimentoValueConverter : ValueConverter<CodigoTermoConsentimento, string>
{
    public CodigoTermoConsentimentoValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoTermoConsentimento Reidratar(string valor)
    {
        Result<CodigoTermoConsentimento> resultado = CodigoTermoConsentimento.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoTermoConsentimento)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}

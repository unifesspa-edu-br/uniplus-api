namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoBanca ↔ string (varchar). O comprimento da coluna é definido na
// própria propriedade via HasMaxLength no IEntityTypeConfiguration. A reidratação
// falha explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo
// da aplicação, em vez do NullReferenceException tardio de
// `CodigoBanca.Criar(v).Value!`.
public sealed class CodigoBancaValueConverter : ValueConverter<CodigoBanca, string>
{
    public CodigoBancaValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoBanca Reidratar(string valor)
    {
        Result<CodigoBanca> resultado = CodigoBanca.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoBanca)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}

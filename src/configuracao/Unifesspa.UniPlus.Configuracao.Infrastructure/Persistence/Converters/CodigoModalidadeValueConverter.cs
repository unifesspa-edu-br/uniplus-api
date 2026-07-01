namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoModalidade ↔ string (varchar). O comprimento da coluna é definido na
// própria propriedade via HasMaxLength no IEntityTypeConfiguration. A reidratação
// falha explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo
// da aplicação, em vez do NullReferenceException tardio de
// `CodigoModalidade.Criar(v).Value!`.
public sealed class CodigoModalidadeValueConverter : ValueConverter<CodigoModalidade, string>
{
    public CodigoModalidadeValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoModalidade Reidratar(string valor)
    {
        Result<CodigoModalidade> resultado = CodigoModalidade.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoModalidade)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia NaturezaLegal ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (COTA_RESERVADA…), não o nome PascalCase do enum. O comprimento da coluna é
// definido na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação, em vez de um valor de enum silenciosamente inválido.
public sealed class NaturezaLegalValueConverter : ValueConverter<NaturezaLegal, string>
{
    public NaturezaLegalValueConverter()
        : base(
            natureza => NaturezasLegais.ParaTokenCanonico(natureza),
            token => Reidratar(token))
    {
    }

    private static NaturezaLegal Reidratar(string token)
    {
        if (!NaturezasLegais.TryAnalisar(token, out NaturezaLegal natureza))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(NaturezaLegal)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return natureza;
    }
}

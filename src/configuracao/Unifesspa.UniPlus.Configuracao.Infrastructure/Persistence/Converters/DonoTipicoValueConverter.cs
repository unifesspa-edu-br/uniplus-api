namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia DonoTipico ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (CEPS, CRCA, MEC, CONSEPE), não o nome PascalCase do enum. O comprimento da
// coluna é definido na própria propriedade via HasMaxLength no
// IEntityTypeConfiguration. A reidratação falha explicitamente (com contexto)
// caso a coluna seja corrompida fora do fluxo da aplicação, em vez de um valor
// de enum silenciosamente inválido.
public sealed class DonoTipicoValueConverter : ValueConverter<DonoTipico, string>
{
    public DonoTipicoValueConverter()
        : base(
            dono => DonosTipicos.ParaTokenCanonico(dono),
            token => Reidratar(token))
    {
    }

    private static DonoTipico Reidratar(string token)
    {
        if (!DonosTipicos.TryAnalisar(token, out DonoTipico dono))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(DonoTipico)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return dono;
    }
}

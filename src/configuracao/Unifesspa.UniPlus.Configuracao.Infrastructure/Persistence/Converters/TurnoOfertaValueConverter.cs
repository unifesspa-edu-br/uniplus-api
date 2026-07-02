namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia TurnoOferta ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (MATUTINO…), não o nome PascalCase do enum. Aplicado a uma propriedade nullable
// (TurnoOferta?) — o EF encapsula null automaticamente; este converter só vê
// valores não-nulos. A reidratação falha explicitamente (com contexto) caso a
// coluna seja corrompida fora do fluxo da aplicação.
public sealed class TurnoOfertaValueConverter : ValueConverter<TurnoOferta, string>
{
    public TurnoOfertaValueConverter()
        : base(
            turno => TurnosOferta.ParaTokenCanonico(turno),
            token => Reidratar(token))
    {
    }

    private static TurnoOferta Reidratar(string token)
    {
        if (!TurnosOferta.TryAnalisar(token, out TurnoOferta turno))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(TurnoOferta)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return turno;
    }
}

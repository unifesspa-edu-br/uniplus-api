namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia RegimeDeTurno ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (REGULAR, INTEGRAL), não o nome PascalCase do enum. A reidratação falha
// explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo da
// aplicação — o mesmo critério do converter de turno.
public sealed class RegimeDeTurnoValueConverter : ValueConverter<RegimeDeTurno, string>
{
    public RegimeDeTurnoValueConverter()
        : base(
            regime => RegimesDeTurno.ParaTokenCanonico(regime),
            token => Reidratar(token))
    {
    }

    private static RegimeDeTurno Reidratar(string token)
    {
        if (!RegimesDeTurno.TryAnalisar(token, out RegimeDeTurno regime))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(RegimeDeTurno)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return regime;
    }
}

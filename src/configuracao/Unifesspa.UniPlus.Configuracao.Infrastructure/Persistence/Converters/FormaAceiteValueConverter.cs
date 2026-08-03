namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia FormaAceite ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (A_DEFINIR…), não o nome PascalCase do enum. A reidratação falha explicitamente
// (com contexto) caso a coluna seja corrompida fora do fluxo da aplicação.
public sealed class FormaAceiteValueConverter : ValueConverter<FormaAceite, string>
{
    public FormaAceiteValueConverter()
        : base(
            formaAceite => FormasAceite.ParaTokenCanonico(formaAceite),
            token => Reidratar(token))
    {
    }

    private static FormaAceite Reidratar(string token)
    {
        if (!FormasAceite.TryAnalisar(token, out FormaAceite formaAceite))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(FormaAceite)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return formaAceite;
    }
}

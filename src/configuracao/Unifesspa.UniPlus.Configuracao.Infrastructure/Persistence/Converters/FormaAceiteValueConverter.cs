namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia FormaAceite ↔ string (varchar) usando o código canônico UPPER_SNAKE
// (A_DEFINIR…), não o nome PascalCase do enum. A reidratação falha explicitamente
// (com contexto) caso a coluna seja corrompida fora do fluxo da aplicação.
public sealed class FormaAceiteValueConverter : ValueConverter<FormaAceite, string>
{
    public FormaAceiteValueConverter()
        : base(
            formaAceite => FormasAceite.ParaTokenCanonico(formaAceite),
            codigo => Reidratar(codigo))
    {
    }

    private static FormaAceite Reidratar(string codigo)
    {
        if (!FormasAceite.TryAnalisar(codigo, out FormaAceite formaAceite))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(FormaAceite)}: '{codigo}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return formaAceite;
    }
}

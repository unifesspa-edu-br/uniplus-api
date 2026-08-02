namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia Abrangencia ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (NACIONAL…), não o nome PascalCase do enum. A reidratação falha explicitamente
// (com contexto) caso a coluna seja corrompida fora do fluxo da aplicação.
public sealed class AbrangenciaValueConverter : ValueConverter<Abrangencia, string>
{
    public AbrangenciaValueConverter()
        : base(
            abrangencia => Abrangencias.ParaTokenCanonico(abrangencia),
            token => Reidratar(token))
    {
    }

    private static Abrangencia Reidratar(string token)
    {
        if (!Abrangencias.TryAnalisar(token, out Abrangencia abrangencia))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(Abrangencia)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return abrangencia;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia OrigemDataFase ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (PROPRIA, DELEGADA), não o nome PascalCase do enum. O comprimento da coluna é
// definido na própria propriedade via HasMaxLength no IEntityTypeConfiguration.
// A reidratação falha explicitamente (com contexto) caso a coluna seja
// corrompida fora do fluxo da aplicação, em vez de um valor de enum
// silenciosamente inválido.
public sealed class OrigemDataFaseValueConverter : ValueConverter<OrigemDataFase, string>
{
    public OrigemDataFaseValueConverter()
        : base(
            origem => OrigensDataFase.ParaTokenCanonico(origem),
            token => Reidratar(token))
    {
    }

    private static OrigemDataFase Reidratar(string token)
    {
        if (!OrigensDataFase.TryAnalisar(token, out OrigemDataFase origem))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(OrigemDataFase)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return origem;
    }
}

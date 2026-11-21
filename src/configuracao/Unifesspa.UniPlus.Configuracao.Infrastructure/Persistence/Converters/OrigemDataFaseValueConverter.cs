namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia OrigemDataFase ↔ string (varchar) usando o código canônico UPPER_SNAKE
// (NO_EDITAL, NA_EXECUCAO), não o nome PascalCase do enum. O comprimento da coluna é
// definido na própria propriedade via HasMaxLength no IEntityTypeConfiguration.
// A reidratação falha explicitamente (com contexto) caso a coluna seja
// corrompida fora do fluxo da aplicação, em vez de um valor de enum
// silenciosamente inválido.
public sealed class OrigemDataFaseValueConverter : ValueConverter<OrigemDataFase, string>
{
    public OrigemDataFaseValueConverter()
        : base(
            origem => OrigensDataFase.ParaTokenCanonico(origem),
            codigo => Reidratar(codigo))
    {
    }

    private static OrigemDataFase Reidratar(string codigo)
    {
        if (!OrigensDataFase.TryAnalisar(codigo, out OrigemDataFase origem))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(OrigemDataFase)}: '{codigo}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return origem;
    }
}

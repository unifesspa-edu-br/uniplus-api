namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia FormatoPedagogico ↔ string (varchar) usando o código canônico UPPER_SNAKE
// (PRESENCIAL…), não o nome PascalCase do enum. O comprimento da coluna é definido
// na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação, em vez de um valor de enum silenciosamente inválido.
public sealed class FormatoPedagogicoValueConverter : ValueConverter<FormatoPedagogico, string>
{
    public FormatoPedagogicoValueConverter()
        : base(
            formato => FormatosPedagogicos.ParaTokenCanonico(formato),
            codigo => Reidratar(codigo))
    {
    }

    private static FormatoPedagogico Reidratar(string codigo)
    {
        if (!FormatosPedagogicos.TryAnalisar(codigo, out FormatoPedagogico formato))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(FormatoPedagogico)}: '{codigo}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return formato;
    }
}

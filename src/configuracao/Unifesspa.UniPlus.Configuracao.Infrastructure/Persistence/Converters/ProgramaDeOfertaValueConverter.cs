namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia ProgramaDeOferta ↔ string (varchar) usando o código canônico UPPER_SNAKE
// (REGULAR…), não o nome PascalCase do enum. O comprimento da coluna é definido
// na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação, em vez de um valor de enum silenciosamente inválido.
public sealed class ProgramaDeOfertaValueConverter : ValueConverter<ProgramaDeOferta, string>
{
    public ProgramaDeOfertaValueConverter()
        : base(
            programa => ProgramasDeOferta.ParaTokenCanonico(programa),
            codigo => Reidratar(codigo))
    {
    }

    private static ProgramaDeOferta Reidratar(string codigo)
    {
        if (!ProgramasDeOferta.TryAnalisar(codigo, out ProgramaDeOferta programa))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(ProgramaDeOferta)}: '{codigo}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return programa;
    }
}

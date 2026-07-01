namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia RegraRemanejamento ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (SEGUE_CASCATA…), não o nome PascalCase do enum. Aplicado a uma propriedade
// nullable (RegraRemanejamento?) — o EF encapsula null automaticamente; este
// converter só vê valores não-nulos. A reidratação falha explicitamente (com
// contexto) caso a coluna seja corrompida fora do fluxo da aplicação.
public sealed class RegraRemanejamentoValueConverter : ValueConverter<RegraRemanejamento, string>
{
    public RegraRemanejamentoValueConverter()
        : base(
            regra => RegrasRemanejamento.ParaTokenCanonico(regra),
            token => Reidratar(token))
    {
    }

    private static RegraRemanejamento Reidratar(string token)
    {
        if (!RegrasRemanejamento.TryAnalisar(token, out RegraRemanejamento regra))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(RegraRemanejamento)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return regra;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia RegimeDeFuncionamento ↔ string (varchar) usando o token canônico
// UPPER_SNAKE (INTENSIVO, EXTENSIVO), não o nome PascalCase do enum. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação — o mesmo critério do converter de regime de turno.
public sealed class RegimeDeFuncionamentoValueConverter : ValueConverter<RegimeDeFuncionamento, string>
{
    public RegimeDeFuncionamentoValueConverter()
        : base(
            regime => RegimesDeFuncionamento.ParaTokenCanonico(regime),
            token => Reidratar(token))
    {
    }

    private static RegimeDeFuncionamento Reidratar(string token)
    {
        if (!RegimesDeFuncionamento.TryAnalisar(token, out RegimeDeFuncionamento regime))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(RegimeDeFuncionamento)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return regime;
    }
}

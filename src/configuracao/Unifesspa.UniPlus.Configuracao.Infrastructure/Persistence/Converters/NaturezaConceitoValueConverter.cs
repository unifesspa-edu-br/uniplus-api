namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia NaturezaConceito ↔ string (varchar) pelo token UPPER_SNAKE, não pelo nome do enum. A reidratação
// falha com contexto se a coluna for alterada fora do fluxo da aplicação, em vez de produzir um valor de
// enum silenciosamente inválido.
public sealed class NaturezaConceitoValueConverter : ValueConverter<NaturezaConceito, string>
{
    public NaturezaConceitoValueConverter()
        : base(
            natureza => NaturezasConceito.ParaToken(natureza),
            token => Reidratar(token))
    {
    }

    private static NaturezaConceito Reidratar(string token) =>
        NaturezasConceito.TryAnalisarToken(token, out NaturezaConceito natureza)
            ? natureza
            : throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(NaturezaConceito)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
}

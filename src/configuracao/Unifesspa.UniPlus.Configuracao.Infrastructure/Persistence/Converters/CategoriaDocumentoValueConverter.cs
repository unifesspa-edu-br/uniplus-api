namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia CategoriaDocumento ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (RACA_ETNIA…), não o nome PascalCase do enum. O comprimento da coluna é definido
// na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A reidratação
// falha explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo da
// aplicação, em vez de um valor de enum silenciosamente inválido.
public sealed class CategoriaDocumentoValueConverter : ValueConverter<CategoriaDocumento, string>
{
    public CategoriaDocumentoValueConverter()
        : base(
            categoria => CategoriaDocumentos.ParaTokenCanonico(categoria),
            token => Reidratar(token))
    {
    }

    private static CategoriaDocumento Reidratar(string token)
    {
        if (!CategoriaDocumentos.TryAnalisar(token, out CategoriaDocumento categoria))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CategoriaDocumento)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return categoria;
    }
}

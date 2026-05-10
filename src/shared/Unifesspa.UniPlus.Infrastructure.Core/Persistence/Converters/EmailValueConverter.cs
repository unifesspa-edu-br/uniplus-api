namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia Email ↔ string normalizado em lowercase (regra do VO). Tamanho
// máximo recomendado pelo schema = 254 chars (limite prático RFC 5321);
// definido via convenção no ModelBuilder, não aqui no converter.
public sealed class EmailValueConverter : ValueConverter<Email, string>
{
    public EmailValueConverter()
        : base(
            email => email.Valor,
            valor => ValueObjectMaterialization.Reidratar(Email.Criar(valor), nameof(Email)))
    {
    }
}

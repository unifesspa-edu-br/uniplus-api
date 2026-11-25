namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

// A leitura falha com contexto se o slug gravado não respeita a regra do domínio.
public sealed class SlugValueConverter : ValueConverter<Slug, string>
{
    public SlugValueConverter()
        : base(
            slug => slug.Valor,
            valor => ObjetoDeValorGravado.Reidratar(Slug.From(valor), nameof(Slug)))
    {
    }
}

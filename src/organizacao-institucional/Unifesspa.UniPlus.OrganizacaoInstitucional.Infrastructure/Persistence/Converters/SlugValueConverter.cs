namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

// Mapeia Slug <-> string no banco com fail-fast nos dois sentidos.
// Slug inválido no banco indica corrupção — falha alto via InvalidOperationException.
public sealed class SlugValueConverter : ValueConverter<Slug, string>
{
    public SlugValueConverter()
        : base(
            slug => slug.Valor,
            valor => Reidratar(valor))
    {
    }

    private static Slug Reidratar(string valor)
    {
        Result<Slug> resultado = Slug.From(valor);
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar Slug: '{valor}' "
                + $"({resultado.Error!.Code}). "
                + "Verifique se houve alteração manual da coluna.");
        }

        return resultado.Value!;
    }
}

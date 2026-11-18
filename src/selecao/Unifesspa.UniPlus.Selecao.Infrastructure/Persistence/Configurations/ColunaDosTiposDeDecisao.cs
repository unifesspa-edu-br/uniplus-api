namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// A coluna <c>tipos_de_decisao</c> (<c>text[]</c>) da fase e da etapa, mapeada pelo campo que o domínio
/// guarda, e não pela propriedade que ele expõe somente leitura.
/// </summary>
/// <remarks>
/// O domínio altera a lista no lugar (<c>Clear</c> e <c>AddRange</c>), por isso o comparador copia a lista
/// para o snapshot do rastreamento: um snapshot que guardasse a mesma instância nunca veria a mudança.
/// </remarks>
internal static class ColunaDosTiposDeDecisao
{
    private const string CampoDoDominio = "_tiposDeDecisao";
    private const string PropriedadePublicaDoDominio = "TiposDeDecisao";

    internal static readonly ValueComparer<List<string>> Comparador =
        new((a, b) => SaoIguais(a, b), tipos => Codigo(tipos), tipos => Copiar(tipos));

    internal static bool SaoIguais(List<string>? a, List<string>? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.SequenceEqual(b, StringComparer.Ordinal));

    internal static int Codigo(List<string> tipos)
    {
        HashCode codigo = new();
        foreach (string tipo in tipos)
        {
            codigo.Add(tipo, StringComparer.Ordinal);
        }

        return codigo.ToHashCode();
    }

    internal static List<string> Copiar(List<string> tipos) => [.. tipos];

    internal static void MapearTiposDeDecisao<TEntidade>(this EntityTypeBuilder<TEntidade> builder)
        where TEntidade : class
    {
        builder.Ignore(PropriedadePublicaDoDominio);
        builder.Property<List<string>>(CampoDoDominio)
            .HasColumnName("tipos_de_decisao")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]")
            .HasComment("Tipos de decisão que a fase ou a etapa declara produzir, em ordem alfabética ordinal e sem repetição.")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .Metadata.SetValueComparer(Comparador);
    }
}

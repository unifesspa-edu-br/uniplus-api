namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A coluna <c>tipos_de_decisao</c> (<c>text[]</c>) da fase e da etapa, mapeada pelo campo que o domínio
/// guarda, e não pela propriedade que ele expõe somente leitura. Cada tipo é gravado pelo código de contrato do enum.
/// </summary>
internal static class ColunaDosTiposDeDecisao
{
    private const string CampoDoDominio = "_tiposDeDecisao";
    private const string PropriedadePublicaDoDominio = "TiposDeDecisao";

    internal static void MapearTiposDeDecisao<TEntidade>(this EntityTypeBuilder<TEntidade> builder)
        where TEntidade : class
    {
        builder.Ignore(PropriedadePublicaDoDominio);
        // Sem HasColumnType: com ele o provedor monta o elemento pelo tipo da coluna e ignora o conversor do código; o text[] sai do conversor.
        builder.PrimitiveCollection<List<TipoMotivoDeDecisao>>(CampoDoDominio)
            .HasColumnName("tipos_de_decisao")
            .HasDefaultValueSql("'{}'::text[]")
            .HasComment($"Tipos de decisão que a fase ou a etapa declara produzir, sem repetição. Códigos: {string.Join(", ", CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static item => item.Codigo))}.")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

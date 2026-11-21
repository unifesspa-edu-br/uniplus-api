namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Forma das colunas da <see cref="TipoDeBancaCopiado"/> embutida na banca da fase e na banca da etapa, num lugar só: as duas
/// bancas guardam a mesma cópia e não podem divergir na tabela. O código do tipo, o papel e cada tipo de decisão da lista são
/// gravados pelo código de contrato do enum, que a convenção do modelo instala.
/// </summary>
internal static class TipoDeBancaCopiadoOwnedConfiguration
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const string CampoDosTiposDeDecisaoJulgados = "_tiposDeDecisaoJulgados";

    public static OwnedNavigationBuilder<TDono, TipoDeBancaCopiado> ConfigurarTipoDeBancaCopiado<TDono>(
        this OwnedNavigationBuilder<TDono, TipoDeBancaCopiado> tipo)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(tipo);

        tipo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        tipo.Property(t => t.OrigemId).HasColumnName("tipo_banca_origem_id").IsRequired();
        tipo.Property(t => t.Codigo).HasColumnName("codigo").HasMaxLength(CodigoMaxLength).IsRequired();
        tipo.Property(t => t.Nome).HasColumnName("nome").HasMaxLength(NomeMaxLength).IsRequired();
        tipo.Property(t => t.Papel).HasColumnName("papel").HasMaxLength(CodigoMaxLength).IsRequired();
        tipo.Property(t => t.ExigeDuasOuMaisBancas).HasColumnName("exige_duas_ou_mais_bancas").IsRequired();
        tipo.Property(t => t.EncerraRecursoNaPrimeiraInstancia).HasColumnName("encerra_recurso_na_primeira_instancia").IsRequired();
        tipo.Ignore(t => t.TiposDeDecisaoJulgados);
        // Sem HasColumnType: com ele o provedor monta o elemento pelo tipo da coluna e ignora o conversor do código; o text[] sai do conversor.
        tipo.PrimitiveCollection<List<TipoMotivoDeDecisao>>(CampoDosTiposDeDecisaoJulgados)
            .HasColumnName("tipos_de_decisao_julgados")
            .HasDefaultValueSql("'{}'::text[]")
            .HasComment("Tipos de decisão que o tipo de banca julgava quando a banca o copiou, em ordem alfabética ordinal do código e sem repetição.")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        return tipo;
    }
}

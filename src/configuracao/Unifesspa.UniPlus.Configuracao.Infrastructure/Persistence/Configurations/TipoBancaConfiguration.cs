namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

internal sealed class TipoBancaConfiguration : IEntityTypeConfiguration<TipoBanca>
{
    private const int CodigoMaxLength = 60;
    private const int PapelMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int FaseTipicaMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int AuditUserMaxLength = 255;
    private const string CampoDosTiposDeDecisaoJulgados = "_tiposDeDecisaoJulgados";

    public void Configure(EntityTypeBuilder<TipoBanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("tipos_de_banca", ConfigurarChecks);

        builder.HasKey(b => b.Id);

        // Codigo é o tipo do vocabulário, imutável, gravado pelo código de contrato do enum
        // (a convenção do modelo instala o conversor). O CHECK do vocabulário e o índice
        // único parcial protegem a coluna.
        builder.Property(b => b.Codigo)
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        // fase_tipica: rótulo de texto orientativo (NÃO é FK para fases_canonicas).
        builder.Property(b => b.FaseTipica).HasMaxLength(FaseTipicaMaxLength);

        builder.Property(b => b.Descricao).HasMaxLength(DescricaoMaxLength);

        MapearAtuacao(builder);

        // Auditoria (IAuditableEntity)
        builder.Property(b => b.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(b => b.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do código entre tipos de banca vivos (índice parcial).
        builder.HasIndex(b => b.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.TipoDeBancaCodigoVivo);
    }

    // A atuação é um valor do tipo de banca, gravado como as quatro colunas da própria linha (table splitting). O papel é
    // gravado pelo código de contrato (a convenção do modelo instala o conversor), assim como cada tipo de decisão da
    // lista, que a atuação guarda no campo e expõe somente leitura.
    private static void MapearAtuacao(EntityTypeBuilder<TipoBanca> builder)
    {
        builder.OwnsOne(b => b.Atuacao, atuacao =>
        {
            atuacao.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            atuacao.Property(a => a.Papel).HasColumnName("papel").HasMaxLength(PapelMaxLength).IsRequired();
            atuacao.Property(a => a.ExigeDuasOuMaisBancas).HasColumnName("exige_duas_ou_mais_bancas").IsRequired();
            atuacao.Property(a => a.EncerraRecursoNaPrimeiraInstancia)
                .HasColumnName("encerra_recurso_na_primeira_instancia").IsRequired();
            atuacao.Ignore(a => a.TiposDeDecisaoJulgados);
            // Sem HasColumnType: com ele o provedor monta o elemento pelo tipo da coluna e ignora o conversor do código; o text[] sai do conversor.
            atuacao.PrimitiveCollection<List<TipoMotivoDeDecisao>>(CampoDosTiposDeDecisaoJulgados)
                .HasColumnName("tipos_de_decisao_julgados")
                .HasDefaultValueSql("'{}'::text[]")
                .HasComment("Tipos de decisão que o tipo de banca julga, em ordem alfabética ordinal do código e sem repetição; só o papel que avalia o mérito os declara.")
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(b => b.Atuacao).IsRequired();
    }

    private static void ConfigurarChecks(TableBuilder<TipoBanca> table)
    {
        // Domínio fechado do vocabulário de tipos de banca (defesa em profundidade).
        table.HasCheckConstraint(
            "ck_tipos_de_banca_codigo_canonico",
            $"codigo IN ({TokensSql(CatalogoDe<TipoDeBanca>.Todos())})");
        table.HasCheckConstraint(
            "ck_tipos_de_banca_papel",
            $"papel IN ({TokensSql(CatalogoDe<PapelDaBanca>.Todos())})");
        table.HasCheckConstraint(
            "ck_tipos_de_banca_decisoes",
            $"tipos_de_decisao_julgados <@ ARRAY[{TokensSql(CatalogoDe<TipoMotivoDeDecisao>.Todos())}]::text[]");
    }

    private static string TokensSql(IReadOnlyList<CodigoDeclarado> codigos) =>
        string.Join(", ", codigos.Select(declarado => $"'{declarado.Codigo}'"));
}

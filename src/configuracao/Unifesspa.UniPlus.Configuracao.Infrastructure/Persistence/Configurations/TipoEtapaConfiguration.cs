namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class TipoEtapaConfiguration : IEntityTypeConfiguration<TipoEtapa>
{
    private const string JuncaoDosConceitosAdmitidos = "conceitos_do_tipo_de_etapa";

    public void Configure(EntityTypeBuilder<TipoEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        // Um tipo que não compõe nota nem elimina não configura etapa nenhuma — toda etapa
        // declara um caráter, e nenhum sobraria para escolher.
        builder.ToTable("tipos_etapa", t =>
        {
            t.HasCheckConstraint("ck_tipos_etapa_codigo_formato", $"codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
            t.HasCheckConstraint(
                "ck_tipos_etapa_carater_admitido",
                "admite_pontuacao OR admite_eliminacao");

            // A etapa de nota do ENEM compõe a média: um tipo com essa origem que não
            // pontuasse não configuraria etapa nenhuma.
            t.HasCheckConstraint(
                "ck_tipos_etapa_nota_de_origem_no_enem_admite_pontuacao",
                "NOT nota_de_origem_no_enem OR admite_pontuacao");

            // Ninguém corrige a nota que vem do ENEM, então não há correção independente a exigir.
            t.HasCheckConstraint(
                "ck_tipos_etapa_correcao_independente_sem_nota_do_enem",
                "NOT (exige_correcao_independente AND nota_de_origem_no_enem)");
        });
        builder.HasKey(tipo => tipo.Id);
        builder.Property(tipo => tipo.Id).ValueGeneratedNever();
        builder.Property(tipo => tipo.Codigo).HasMaxLength(FormatoDoCodigo.ComprimentoMaximo).IsRequired();
        builder.Property(tipo => tipo.Nome).HasMaxLength(200).IsRequired();
        builder.Property(tipo => tipo.Descricao).HasMaxLength(1000);
        builder.Property(tipo => tipo.Ativo).IsRequired();
        builder.Property(tipo => tipo.AdmitePontuacao).IsRequired();
        builder.Property(tipo => tipo.AdmiteEliminacao).IsRequired();
        builder.Property(tipo => tipo.ExigeCorrecaoIndependente).IsRequired();

        // Token de concorrência otimista na coluna de sistema `xmin` do Postgres (shadow property
        // `uint` + IsRowVersion, sem coluna nem migration própria). O PUT substitui o conjunto de
        // conceitos admitidos; sem o token, duas edições concorrentes do mesmo tipo gravariam sem
        // saber uma da outra. Quem confirma por último recebe DbUpdateConcurrencyException (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
        builder.Property(tipo => tipo.NotaDeOrigemNoEnem)
            .IsRequired()
            .HasComment("Nota das etapas deste tipo vem do ENEM; definido só pela carga do cadastro, nunca pela API.");
        builder.Property(tipo => tipo.CreatedBy).HasMaxLength(255);
        builder.Property(tipo => tipo.UpdatedBy).HasMaxLength(255);

        // A unicidade abrange também itens inativos: código de domínio nunca é reutilizado.
        builder.HasIndex(tipo => tipo.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_tipos_etapa_codigo");
        builder.HasIndex(tipo => tipo.Ativo)
            .HasDatabaseName("ix_tipos_etapa_ativo");

        ConfigurarConceitosAdmitidos(builder);
    }

    // O conjunto de conceitos admitidos é substituído por inteiro a cada edição. A junção só guarda o par:
    // apagar um tipo de etapa leva os pares, e um tipo de conceito com par não pode ser apagado — ele é
    // desativado, nunca removido.
    private static void ConfigurarConceitosAdmitidos(EntityTypeBuilder<TipoEtapa> builder)
    {
        builder.HasMany(tipo => tipo.ConceitosAdmitidos).WithMany().UsingEntity<Dictionary<string, object>>(
            JuncaoDosConceitosAdmitidos,
            conceito => conceito.HasOne<TipoConceito>().WithMany().HasForeignKey("TipoConceitoId").HasConstraintName("fk_conceitos_do_tipo_de_etapa_tipo_conceito").OnDelete(DeleteBehavior.Restrict),
            tipo => tipo.HasOne<TipoEtapa>().WithMany().HasForeignKey("TipoEtapaId").HasConstraintName("fk_conceitos_do_tipo_de_etapa_tipo_etapa").OnDelete(DeleteBehavior.Cascade),
            juncao =>
            {
                juncao.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
                juncao.ToTable(JuncaoDosConceitosAdmitidos, tabela => tabela.HasComment(
                    "Tipos de conceito com que as etapas de um tipo de etapa podem ser avaliadas."));
                juncao.HasKey("TipoEtapaId", "TipoConceitoId").HasName(RestricoesDoBanco.TipoDeEtapaConceitoAdmitidoChave);
                juncao.HasIndex("TipoConceitoId").HasDatabaseName("ix_conceitos_do_tipo_de_etapa_tipo_conceito_id");
                juncao.IndexerProperty<Guid>("TipoEtapaId").HasComment("Tipo de etapa que admite o conceito.");
                juncao.IndexerProperty<Guid>("TipoConceitoId").HasComment("Tipo de conceito admitido pelo tipo de etapa.");
            });
    }
}

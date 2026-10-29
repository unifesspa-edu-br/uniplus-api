namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Configuração EF Core da concessão de acesso.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConcessaoConfiguration : IEntityTypeConfiguration<Concessao>
{
    private const int IdentidadeMaxLength = 255;
    private const int TipoMaxLength = 20;
    private const int MotivoMaxLength = 500;

    private static readonly ValueConverter<TipoDeEscopo, string> EscopoConverter = new(
        tipo => tipo.ToCodigo(),
        codigo => TipoDeEscopoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<TipoDeOrigem, string> OrigemConverter = new(
        tipo => tipo.ToCodigo(),
        codigo => TipoDeOrigemCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<Concessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O banco confere o que o agregado já garante, para que nenhuma gravação direta deixe a
        // revogação pela metade, o escopo incoerente ou a vigência anterior à concessão.
        builder.ToTable("concessoes", tabela =>
        {
            tabela.HasCheckConstraint(
                "ck_concessoes_revogacao_completa",
                "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NULL)"
                + " OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_sub IS NOT NULL AND motivo_revogacao IS NOT NULL)");
            tabela.HasCheckConstraint(
                "ck_concessoes_escopo_unidade",
                "(escopo_tipo = 'unidade') = (unidade_id IS NOT NULL)");
            tabela.HasCheckConstraint(
                "ck_concessoes_validade_posterior",
                "valida_ate IS NULL OR valida_ate > concedida_em");
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BeneficiarioEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(c => c.BeneficiarioSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(c => c.ConcedidaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(c => c.ConcedidaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(c => c.RevogadaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(c => c.RevogadaPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(c => c.MotivoRevogacao).HasMaxLength(MotivoMaxLength);

        builder.Property(c => c.EscopoTipo).HasConversion(EscopoConverter).HasMaxLength(TipoMaxLength).IsRequired();
        builder.Property(c => c.OrigemTipo).HasConversion(OrigemConverter).HasMaxLength(TipoMaxLength).IsRequired();

        // A unidade é de outro módulo (ADR-0087): sem chave estrangeira entre schemas.
        builder.Property(c => c.UnidadeId);
        builder.Property(c => c.OrigemId).IsRequired();

        // O perfil é do mesmo módulo. A chave estrangeira impede apagar o perfil e mantém a
        // concessão sempre apontando para um perfil que existe.
        builder.HasOne<Perfil>()
            .WithMany()
            .HasForeignKey(c => c.PerfilId)
            .OnDelete(DeleteBehavior.Restrict);

        // Não existem duas concessões não revogadas iguais, vigentes ou vencidas: a revogação é o
        // único encerramento gravado, e quem concede de novo revoga a vencida antes. A unidade
        // nula (escopo da instituição) conta como igual a outra nula, o que o índice único não
        // faria por padrão.
        builder.HasIndex(
                c => new { c.BeneficiarioEmissor, c.BeneficiarioSub, c.PerfilId, c.EscopoTipo, c.UnidadeId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("revogada_em IS NULL")
            .HasDatabaseName("ux_concessoes_ativa_unica");

        // Reprocessar a mesma origem não duplica. A instalação gera duas concessões com a mesma
        // origem, por isso o beneficiário entra na chave.
        builder.HasIndex(
                c => new { c.OrigemTipo, c.OrigemId, c.BeneficiarioEmissor, c.BeneficiarioSub })
            .IsUnique()
            .HasDatabaseName("ux_concessoes_origem");

        // Contagem de gestores de acesso.
        builder.HasIndex(c => c.PerfilId)
            .HasFilter("revogada_em IS NULL")
            .HasDatabaseName("ix_concessoes_perfil_ativas");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }
}

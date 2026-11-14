namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Persistência da vaga que um objeto reserva para uma única linhagem de atos de
/// um tipo <c>unico_por_objeto</c> (ADR-0107). O índice único é a garantia dura;
/// a consulta prévia do handler só existe para dar mensagem legível.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VagaDeLinhagemConfiguration : IEntityTypeConfiguration<VagaDeLinhagem>
{
    private const int EntidadeTipoMaxLength = 60;
    private const int TipoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<VagaDeLinhagem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable(
            "vagas_de_linhagem",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_vagas_de_linhagem_entidade_tipo_formato",
                    "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");

                t.HasCheckConstraint(
                    "ck_vagas_de_linhagem_entidade_id_nao_zero",
                    "entidade_id <> '00000000-0000-0000-0000-000000000000'");

                t.HasCheckConstraint(
                    "ck_vagas_de_linhagem_raiz_nao_zero",
                    "raiz_id <> '00000000-0000-0000-0000-000000000000'");
            });

        builder.HasKey(l => l.Id);

        builder.Property(l => l.EntidadeTipo).HasMaxLength(EntidadeTipoMaxLength).IsRequired();
        builder.Property(l => l.EntidadeId).IsRequired();
        builder.Property(l => l.TipoCodigo).HasMaxLength(TipoCodigoMaxLength).IsRequired();
        builder.Property(l => l.RaizId).IsRequired();
        builder.Property(l => l.AtoId).IsRequired();

        // FK para o ato que abriu a vaga — intra-módulo, como a self-ref da cadeia de
        // retificação (a ADR-0061 só proíbe FK atravessando a fronteira de outro
        // módulo). Restrict: coerente com o append-only, que já bloqueia DELETE.
        // Não há FK para a entidade vinculada, e não pode haver: o objeto é opaco.
        builder.HasOne<AtoNormativo>()
            .WithMany()
            .HasForeignKey(l => l.AtoId)
            .HasConstraintName("fk_vagas_de_linhagem_ato")
            .OnDelete(DeleteBehavior.Restrict);

        // A raiz da linhagem é um ato, não um identificador solto: sem esta chave, uma
        // linha forjada reservaria a vaga em nome de uma linhagem inexistente, e nada a
        // desmentiria. Que ela seja a raiz VERDADEIRA da cadeia do ato é o que o trigger
        // de coerência verifica — a chave só garante que o ato existe.
        builder.HasOne<AtoNormativo>()
            .WithMany()
            .HasForeignKey(l => l.RaizId)
            .HasConstraintName("fk_vagas_de_linhagem_raiz")
            .OnDelete(DeleteBehavior.Restrict);

        // A garantia dura da invariante: um objeto tem, no máximo, UMA linhagem viva de
        // atos de um dado tipo único por objeto. Índice único (não parcial): a linha só
        // existe para tipo único por objeto — quem decide inseri-la é o handler, e um
        // tipo que não é único por objeto nunca chega aqui. Fecha a corrida
        // check-then-act entre duas transações que reservem a mesma vaga.
        builder.HasIndex(l => new { l.EntidadeTipo, l.EntidadeId, l.TipoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_vagas_de_linhagem_objeto_tipo");
    }
}

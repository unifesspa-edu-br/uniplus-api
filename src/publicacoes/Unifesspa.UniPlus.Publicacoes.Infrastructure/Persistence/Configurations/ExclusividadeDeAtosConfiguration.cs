namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Application.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Persistência da exclusividade de atos: a reserva que um objeto faz para uma única cadeia de
/// atos de um tipo <c>unico_por_objeto</c> (ADR-0107). O índice único é a garantia dura;
/// a consulta prévia do handler só existe para dar mensagem legível.
/// </summary>
public sealed class ExclusividadeDeAtosConfiguration : IEntityTypeConfiguration<VagaDeLinhagem>
{
    private const int EntidadeTipoMaxLength = TipoDaEntidadeVinculada.TamanhoMaximo;
    private const int TipoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<VagaDeLinhagem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable(
            "exclusividades_de_atos",
            t =>
            {
                t.HasComment("Exclusividade de atos: reserva que um objeto faz para uma única cadeia de retificação de atos de um tipo único por objeto.");

                t.HasCheckConstraint("ck_exclusividades_de_atos_tipo_codigo_formato", $"tipo_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
                t.HasCheckConstraint(
                    "ck_exclusividades_de_atos_entidade_tipo_formato",
                    $"entidade_tipo ~ '{TipoDaEntidadeVinculada.Padrao}'");

                t.HasCheckConstraint(
                    "ck_exclusividades_de_atos_entidade_id_nao_zero",
                    "entidade_id <> '00000000-0000-0000-0000-000000000000'");

                t.HasCheckConstraint(
                    "ck_exclusividades_de_atos_ato_inicial_nao_zero",
                    "ato_inicial_id <> '00000000-0000-0000-0000-000000000000'");
            });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasComment("Identificador da exclusividade de atos.");

        builder.Property(l => l.EntidadeTipo).HasMaxLength(EntidadeTipoMaxLength).IsRequired()
            .HasComment("Tipo da entidade tratada, opaco para o módulo (por exemplo, PROCESSO_SELETIVO).");
        builder.Property(l => l.EntidadeId).IsRequired()
            .HasComment("Identificador da entidade tratada, opaco para o módulo.");
        builder.Property(l => l.TipoCodigo).HasMaxLength(TipoCodigoMaxLength).IsRequired()
            .HasComment("Código do tipo de ato, copiado do ato que abriu a exclusividade.");
        builder.Property(l => l.RaizId).HasColumnName("ato_inicial_id").IsRequired()
            .HasComment("Ato inicial da cadeia de retificação que detém a exclusividade: identifica a cadeia inteira.");
        builder.Property(l => l.AtoId).IsRequired()
            .HasComment("Ato que abriu a exclusividade. Pode ser uma retificação, quando o ato inicial foi publicado sem vínculo com o objeto.");

        // FK para o ato que abriu a exclusividade — intra-módulo, como a self-ref da cadeia de
        // retificação (a ADR-0061 só proíbe FK atravessando a fronteira de outro
        // módulo). Restrict: coerente com o append-only, que já bloqueia DELETE.
        // Não há FK para a entidade vinculada, e não pode haver: o objeto é opaco.
        builder.HasOne<AtoNormativo>()
            .WithMany()
            .HasForeignKey(l => l.AtoId)
            .HasConstraintName("fk_exclusividades_de_atos_ato")
            .OnDelete(DeleteBehavior.Restrict);

        // O ato inicial da cadeia é um ato, não um identificador solto: sem esta chave, uma
        // linha forjada reservaria a exclusividade em nome de uma cadeia inexistente, e nada a
        // desmentiria. Que ele seja o ato inicial VERDADEIRO da cadeia do ato é o que o trigger
        // de coerência verifica — a chave só garante que o ato existe.
        builder.HasOne<AtoNormativo>()
            .WithMany()
            .HasForeignKey(l => l.RaizId)
            .HasConstraintName("fk_exclusividades_de_atos_ato_inicial")
            .OnDelete(DeleteBehavior.Restrict);

        // A garantia dura da invariante: um objeto tem, no máximo, UMA cadeia viva de
        // atos de um dado tipo único por objeto. Índice único (não parcial): a linha só
        // existe para tipo único por objeto — quem decide inseri-la é o handler, e um
        // tipo que não é único por objeto nunca chega aqui. Fecha a corrida
        // check-then-act entre duas transações que reservem a mesma exclusividade.
        builder.HasIndex(l => new { l.EntidadeTipo, l.EntidadeId, l.TipoCodigo })
            .IsUnique()
            .HasDatabaseName(RestricoesDoBanco.ExclusividadeDeAtosObjetoTipoUnico);
    }
}

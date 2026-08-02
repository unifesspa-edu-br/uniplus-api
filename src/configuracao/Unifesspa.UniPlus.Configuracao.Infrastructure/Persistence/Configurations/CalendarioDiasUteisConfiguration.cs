namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CalendarioDiasUteisConfiguration : IEntityTypeConfiguration<CalendarioDiasUteis>
{
    private const int VersaoDatasetMaxLength = 60;

    public void Configure(EntityTypeBuilder<CalendarioDiasUteis> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("calendario_dias_uteis");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.VersaoDataset).HasMaxLength(VersaoDatasetMaxLength).IsRequired();
        builder.Property(c => c.Vigente).IsRequired();

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Dias não úteis do dataset: append-only enquanto o dataset existe — a lista
        // só nasce inteira no Criar, nunca ganha/perde linha depois (issue #1016 §2).
        // ClientNoAction (não Restrict/Cascade): com a coleção já rastreada,
        // Restrict/NoAction lançam "required relationship severed" ao marcar o
        // CalendarioDiasUteis como Deleted, ANTES de o interceptor convertê-lo em
        // soft-delete; ClientNoAction deixa o interceptor converter em UPDATE e os
        // dias não úteis permanecem intactos (mesmo padrão de Unidade.Historico).
        builder.HasMany(c => c.DiasNaoUteis)
            .WithOne()
            .HasForeignKey(d => d.CalendarioDiasUteisId)
            .OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(c => c.DiasNaoUteis)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // No máximo um dataset vivo é vigente: EXCLUDE constraint GiST DEFERRABLE
        // INITIALLY DEFERRED (ex_calendario_dias_uteis_vigente_unico), criada via SQL
        // cru na migration — de propósito AUSENTE daqui como HasIndex().IsUnique(),
        // mesmo padrão de ex_nos_exigencia_irmaos_ordem (NoExigenciaConfiguration.cs) e
        // ex_tipo_ato_publicado_codigo_vigencia (TipoAtoPublicadoConfiguration.cs), que o
        // Fluent API do EF Core não modela. Um índice único comum é verificado
        // por-statement: MarcarVigenteCalendarioDiasUteisCommandHandler desmarca o
        // vigente anterior e marca o novo na MESMA SaveChangesAsync, e o EF Core não
        // garante que o UPDATE de desmarcar execute antes do de marcar — com um índice
        // não-deferível, a ordem inversa faz o segundo UPDATE colidir com o primeiro
        // (23505) mesmo a transação terminando num estado final válido. Deferir a
        // checagem para o COMMIT resolve sem separar a troca de vigente em duas
        // chamadas a SaveChangesAsync. A invariante primária continua no handler, que
        // localiza e desmarca o vigente anterior na mesma transação — a constraint de
        // banco é defesa de última linha.

        builder.HasIndex(c => c.VersaoDataset)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_calendario_dias_uteis_versao_dataset");
    }
}

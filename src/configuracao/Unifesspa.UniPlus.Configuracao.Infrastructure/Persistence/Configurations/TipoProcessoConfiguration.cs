namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada pelo EF Core via reflection.")]
internal sealed class TipoProcessoConfiguration : IEntityTypeConfiguration<TipoProcesso>
{
    public void Configure(EntityTypeBuilder<TipoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tipos_processo");
        builder.HasKey(tipo => tipo.Id);
        builder.Property(tipo => tipo.Id).ValueGeneratedNever();
        builder.Property(tipo => tipo.Codigo).HasMaxLength(64).IsRequired();
        builder.Property(tipo => tipo.Nome).HasMaxLength(200).IsRequired();
        builder.Property(tipo => tipo.Descricao).HasMaxLength(1000);
        builder.Property(tipo => tipo.Ativo).IsRequired();
        // Token de concorrência otimista mapeado para a coluna de sistema `xmin` do
        // Postgres (shadow property `uint` + IsRowVersion — convenção do provider
        // Npgsql, sem coluna nem migration própria). Sem ele, duas reativações
        // concorrentes do mesmo tipo leem `Ativo == false`, ambas atravessam a guarda
        // do agregado e ambas gravam: as duas responderiam 204, e a recusa de reativar
        // o que já está ativo — que é regra do cadastro — só valeria fora de corrida.
        // Com xmin, quem confirma por último recebe DbUpdateConcurrencyException.
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(tipo => tipo.CreatedBy).HasMaxLength(255);
        builder.Property(tipo => tipo.UpdatedBy).HasMaxLength(255);

        // A unicidade abrange também itens inativos: código de domínio nunca é reutilizado.
        builder.HasIndex(tipo => tipo.Codigo)
            .IsUnique()
            .HasDatabaseName("ix_tipos_processo_codigo");
        builder.HasIndex(tipo => tipo.Ativo)
            .HasDatabaseName("ix_tipos_processo_ativo");
    }
}

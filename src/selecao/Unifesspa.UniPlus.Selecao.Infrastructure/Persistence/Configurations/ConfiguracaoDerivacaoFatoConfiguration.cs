namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoDerivacaoFato"/> (Story #927) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro. Substituível por inteiro junto com o processo,
/// mesmo padrão de <c>FatoColetado</c>.
/// </summary>
public sealed class ConfiguracaoDerivacaoFatoConfiguration : IEntityTypeConfiguration<ConfiguracaoDerivacaoFato>
{
    private const int FatoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<ConfiguracaoDerivacaoFato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_derivacao_fato");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CodigoFato).HasMaxLength(FatoCodigoMaxLength).IsRequired();

        // Um fato derivado tem no máximo uma configuração de derivação por processo — invariante do
        // agregado, garantida também pelo índice contra qualquer caminho que não passe por ele.
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.CodigoFato })
            .IsUnique()
            .HasDatabaseName("ux_configuracoes_derivacao_fato_processo_fato");

        builder.HasMany(c => c.Regras)
            .WithOne()
            .HasForeignKey(r => r.ConfiguracaoDerivacaoFatoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Regras)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

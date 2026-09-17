namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal sealed class CertameDivulgadoConfiguration : IEntityTypeConfiguration<CertameDivulgado>
{
    public void Configure(EntityTypeBuilder<CertameDivulgado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("certames_divulgados");

        // O processo É a identidade: um certame tem uma divulgação corrente, nunca duas.
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.NumeroVersao).IsRequired();
        builder.Property(c => c.AtoCriadorId).IsRequired();
        builder.Property(c => c.HashConfiguracao).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(c => c.VersaoProjecao).HasMaxLength(16).IsRequired();
        builder.Property(c => c.InscricoesDe).IsRequired();
        builder.Property(c => c.InscricoesAte).IsRequired();
        builder.Property(c => c.DivulgadoEm).IsRequired();

        // A resposta pública pronta. jsonb, e não texto: mesmo que hoje a leitura devolva o
        // documento inteiro, buscar dentro dele é a próxima necessidade previsível da vitrine.
        builder.Property(c => c.Certame).HasColumnType("jsonb").IsRequired();

        // Índice da vitrine: ordena do prazo mais próximo ao mais distante, com o identificador
        // fechando a ordem total. Aqui ele SERVE a ordenação, ao contrário do arranjo anterior —
        // não há mais expressão sobre parâmetro na primeira coluna, porque a linha só existe quando
        // o certame é público e não há mais nada a filtrar por fora dela.
        builder.HasIndex(c => new { c.InscricoesAte, c.Id })
            .HasDatabaseName("ix_certames_divulgados_prazo");

        // Um ato divulga no máximo um certame — a mesma unicidade que o registro central garante do
        // outro lado, reafirmada aqui para que uma reentrega não produza duas divulgações.
        builder.HasIndex(c => c.AtoCriadorId)
            .IsUnique()
            .HasDatabaseName("ux_certames_divulgados_ato_criador");
    }
}

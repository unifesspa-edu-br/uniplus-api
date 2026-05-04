namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class CandidatoConfiguration : IEntityTypeConfiguration<Candidato>
{
    public void Configure(EntityTypeBuilder<Candidato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidatos");
        builder.HasKey(c => c.Id);

        builder.OwnsOne(c => c.Cpf, cpfBuilder =>
        {
            cpfBuilder.Property(v => v.Valor).HasColumnName("cpf").HasMaxLength(11).IsRequired();
            cpfBuilder.HasIndex(v => v.Valor).IsUnique();
        });

        builder.OwnsOne(c => c.NomeSocial, nsBuilder =>
        {
            nsBuilder.Property(v => v.NomeCivil).HasColumnName("nome_civil").HasMaxLength(300).IsRequired();
            nsBuilder.Property(v => v.Nome).HasColumnName("nome_social").HasMaxLength(300);
        });

        builder.OwnsOne(c => c.Email, emailBuilder =>
        {
            emailBuilder.Property(v => v.Valor).HasColumnName("email").HasMaxLength(320).IsRequired();
        });

        builder.Property(c => c.DataNascimento).IsRequired();
        builder.Property(c => c.Telefone).HasMaxLength(20);

        builder.Ignore(c => c.NomeExibicao);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

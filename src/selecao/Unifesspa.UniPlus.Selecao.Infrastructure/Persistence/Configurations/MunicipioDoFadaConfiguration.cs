namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class MunicipioDoFadaConfiguration : IEntityTypeConfiguration<MunicipioDoFada>
{
    public void Configure(EntityTypeBuilder<MunicipioDoFada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("municipios_do_fada", t => t.HasComment("Municípios da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA), copiados da base legal."));
        builder.HasKey(m => m.Id).HasName("pk_municipios_do_fada");
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(m => m.Id).ValueGeneratedNever().HasComment("Identificador do município na configuração do Fator de Atenuação das Desigualdades de Acesso (FADA).");
        builder.Property(m => m.ConfiguracaoDoFadaId).HasColumnName("configuracao_do_fada_id").HasComment("Identificador da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA).");

        builder.Property(m => m.CodigoIbge).HasComment("Código IBGE do município do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength).IsRequired();
        builder.Property(m => m.Nome).HasComment("Nome do município do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength).IsRequired();
        builder.Property(m => m.Uf).HasComment("Sigla da unidade federativa do município do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasMaxLength(ReferenciaCidadeGeo.UfLength).IsRequired();

        builder.HasIndex(m => m.ConfiguracaoDoFadaId)
            .HasDatabaseName("ix_municipios_do_fada_configuracao_do_fada_id");
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RespostaDaInscricaoRecordConfiguration : IEntityTypeConfiguration<RespostaDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Sensivel);

        builder.Property(r => r.InscricaoId).HasComment("Identificador da inscrição à qual pertence.");
        builder.Property(r => r.OcorrenciaId).HasComment("Identificador da ocorrência de grupo, quando a resposta pertence a um grupo repetível.");
        builder.Property(r => r.Posicao).HasComment("Posição do valor em uma resposta com vários valores; nula para valor único.");
        builder.Property(r => r.FatoCodigo).HasComment("Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(r => r.Origem).HasComment("Origem da resposta: 1 informada pela pessoa candidata ou 2 calculada pelo sistema.");
        builder.Property(r => r.Tipo).HasComment("Tipo do valor: 1 booleano, 2 número, 3 código, 4 texto, 5 data ou 7 município.");
        builder.Property(r => r.ValorBooleano).HasComment("Resposta booleana, quando aplicável.");
        builder.Property(r => r.ValorNumero).HasComment("Resposta numérica, quando aplicável.");
        builder.Property(r => r.ValorCodigo).HasComment("Código da opção ou município respondido, quando aplicável.");
        builder.Property(r => r.ValorTexto).HasComment("Resposta em texto livre, quando aplicável.");
        builder.Property(r => r.ValorData).HasComment("Resposta de data, quando aplicável.");

        builder.ToTable("respostas_da_inscricao", t =>
        {
            t.HasComment("Um valor respondido na inscrição, na coluna do tipo do fato, com a origem: informado pelo candidato ou derivado de outra resposta.");
            t.HasCheckConstraint(
                "ck_respostas_da_inscricao_valor_do_tipo",
                "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN (1, 2, 3, 4, 5, 7) AND ("
                + "valor_cifrado IS NOT NULL"
                + " OR (tipo = 1 AND valor_booleano IS NOT NULL)"
                + " OR (tipo = 2 AND valor_numero IS NOT NULL)"
                + " OR (tipo = 3 AND valor_codigo IS NOT NULL)"
                + " OR (tipo = 4 AND valor_texto IS NOT NULL)"
                + " OR (tipo = 5 AND valor_data IS NOT NULL)"
                + " OR (tipo = 7 AND valor_codigo IS NOT NULL))"
                + " AND (valor_rotulo IS NULL OR (tipo = 7 AND valor_codigo IS NOT NULL))");
            t.HasCheckConstraint("ck_respostas_da_inscricao_origem", "origem IN (1, 2)");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ClassificaComo(ClassificacaoDeProtecao.Interno).ValueGeneratedNever();
        builder.Property(r => r.InscricaoId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(r => r.OcorrenciaId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(r => r.Posicao).ClassificaComo(ClassificacaoDeProtecao.Interno);

        builder.Property(r => r.FatoCodigo).HasColumnName("codigo_do_dado").ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(r => r.Origem).ClassificaComo(ClassificacaoDeProtecao.Interno).HasConversion<int>().IsRequired();
        builder.Property(r => r.Tipo).ClassificaComo(ClassificacaoDeProtecao.Interno).HasConversion<int>().IsRequired();
        builder.Property(r => r.ValorCodigo).HasMaxLength(OpcaoDeclaradaFato.CodigoMaxLength);
        builder.Property(r => r.ValorRotulo)
            .HasComment("O nome que o Geo conferiu para o município de valor_codigo; nulo no rascunho e preenchido na inscrição enviada.");

        // As colunas de valor guardam valores de fatos de classes diferentes, cuja classificação real é a do
        // fato coletado da linha, resolvida pelo projetor: valem a mais alta, o padrão do tipo. Só a chave e
        // o metadado são Interno, para que uma coluna nova herde o padrão seguro.
        builder.Property(r => r.ValorCifrado)
            .HasColumnType("bytea")
            .HasComment("Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OcorrenciaDeGrupoDaInscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.OcorrenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.InscricaoId, r.OcorrenciaId, r.FatoCodigo, r.Posicao })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_respostas_da_inscricao_valor");

        // A classificação e a habilitação filtram candidatos por fato e valor; texto livre não é filtro.
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorCodigo })
            .HasFilter("valor_codigo IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_codigo");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorBooleano })
            .HasFilter("valor_booleano IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_booleano");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorNumero })
            .HasFilter("valor_numero IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_numero");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorData })
            .HasFilter("valor_data IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_data");
    }
}

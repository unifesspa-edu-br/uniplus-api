namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VinculoDiscenteRecordConfiguration : IEntityTypeConfiguration<VinculoDiscenteRecord>
{
    public void Configure(EntityTypeBuilder<VinculoDiscenteRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vinculo_discente", t => t.HasComment(
            "Réplica local dos vínculos de discentes sincronizados do SIGAA (ADR-0121) — " +
            "snapshot desnormalizado, sem referência viva a outras tabelas."));

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasComment("Identificador interno (UUIDv7) do registro — não confundir com id_discente_sigaa, a chave natural do SIGAA.");

        builder.Property(v => v.IdDiscenteSigaa)
            .HasComment("Identificador do discente no SIGAA (chave natural do módulo) — usado para localizar e fazer upsert durante a sincronização.");

        builder.Property(v => v.Matricula)
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Matrícula do discente na instituição.");

        // Envelope autenticado (nonce + tag + dados cifrados) — nunca os 11 dígitos em
        // texto claro. bytea evita a expansão de ~33% de uma codificação Base64 (ADR-0121).
        builder.Property(v => v.CpfCiphertext)
            .HasColumnType("bytea")
            .IsRequired()
            .HasComment("CPF cifrado em repouso (AES-GCM, ADR-0121) — envelope autenticado (nonce + tag + dado); nunca texto claro.");

        builder.Property(v => v.Nome)
            .HasMaxLength(250)
            .IsRequired()
            .HasComment("Nome do discente.");

        builder.Property(v => v.Nivel)
            .HasMaxLength(5)
            .IsRequired()
            .HasComment("Nível de ensino do vínculo (ex.: G para graduação) — vocabulário do SIGAA.");

        builder.Property(v => v.CursoId)
            .HasComment("Identificador do curso no SIGAA.");

        builder.Property(v => v.CursoNome)
            .HasMaxLength(250)
            .IsRequired()
            .HasComment("Nome do curso.");

        builder.Property(v => v.CursoCodigoEmec)
            .HasMaxLength(20)
            .HasComment("Código e-MEC do curso, quando disponível.");

        builder.Property(v => v.CursoUnidadeId)
            .HasComment("Identificador da unidade acadêmica responsável pelo curso, no SIGAA.");

        builder.Property(v => v.CursoUnidadeNome)
            .HasMaxLength(250)
            .IsRequired()
            .HasComment("Nome da unidade acadêmica responsável pelo curso.");

        builder.Property(v => v.SituacaoId)
            .HasComment("Identificador da situação acadêmica do discente no SIGAA.");

        builder.Property(v => v.SituacaoDescricao)
            .HasMaxLength(250)
            .IsRequired()
            .HasComment("Descrição da situação acadêmica (ex.: Matriculado, Concluído).");

        builder.Property(v => v.SituacaoVinculo)
            .HasMaxLength(100)
            .HasComment("Qualificador de vínculo associado à situação, no vocabulário do SIGAA.");

        builder.Property(v => v.AnoIngresso)
            .HasComment("Ano de ingresso do discente no curso.");

        builder.Property(v => v.PeriodoIngresso)
            .HasComment("Período letivo de ingresso do discente no curso.");

        builder.Property(v => v.ResumoDoConteudo)
            .HasMaxLength(64)
            .IsRequired()
            .HasComment(
                "Resumo do conteúdo trazido do SIGAA na última sincronização — permite reconhecer "
                + "que o vínculo não mudou e poupar a reescrita. Não cobre o CPF: como as demais "
                + "colunas ficam legíveis, um resumo que o cobrisse permitiria recuperá-lo por "
                + "tentativa e erro, desfazendo a cifra em repouso.");

        // Chave natural do módulo (id_discente do SIGAA) — a réplica localiza e faz
        // upsert por este identificador, nunca por CPF (ADR-0121: sem índice de
        // igualdade sobre o campo cifrado, pois o módulo não precisa buscar por CPF).
        builder.HasIndex(v => v.IdDiscenteSigaa)
            .IsUnique()
            .HasDatabaseName("ix_vinculo_discente_id_discente_sigaa");
    }
}

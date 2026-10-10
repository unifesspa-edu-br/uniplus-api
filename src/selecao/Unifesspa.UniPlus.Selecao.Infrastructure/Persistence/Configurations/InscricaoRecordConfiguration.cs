namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class InscricaoRecordConfiguration : IEntityTypeConfiguration<InscricaoRecord>
{
    public void Configure(EntityTypeBuilder<InscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("inscricoes", t => t.HasComment(
            "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e " +
            "processo. As respostas ficam em respostas_da_inscricao e nas tabelas vizinhas."));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.ProcessoSeletivoId)
            .HasComment("O processo seletivo desta inscrição.");

        // Envelope autenticado (nonce + tag + dado) — nunca os 11 dígitos em texto claro. bytea
        // evita a expansão de ~33% de uma codificação Base64 (ADR-0121, mesmo motivo de
        // VinculoDiscenteRecord.CpfCiphertext).
        builder.Property(i => i.CpfCiphertext)
            .HasColumnType("bytea")
            .IsRequired()
            .HasComment("CPF cifrado em repouso (AES-GCM, ADR-0121) — envelope autenticado; nunca texto claro.");

        // Índice cego (HMAC) — determinístico por construção, nunca decifrado para buscar ou
        // garantir unicidade (ADR-0121, emenda de 2026-10-10).
        builder.Property(i => i.CpfIndice)
            .HasColumnType("bytea")
            .IsRequired()
            .HasComment("Índice cego (HMAC-SHA256) do CPF — sustenta a busca pelo dono e o índice único por processo, sem decifrar a tabela.");

        builder.Property(i => i.Sub)
            .HasMaxLength(255)
            .IsRequired()
            .HasComment("O sub do token de quem iniciou a inscrição — nunca troca de dono.");

        builder.Property(i => i.EmissorToken)
            .HasMaxLength(500)
            .IsRequired()
            .HasComment("O emissor (issuer) do token de quem iniciou a inscrição.");

        // Lifecycle enum simples — mesmo padrão de ProcessoSeletivoConfiguration (StatusProcesso).
        builder.Property(i => i.Situacao)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.VersaoFormularioId)
            .HasComment("A versão do formulário divulgada no instante do início (UNI-REQ-0084) — fixa, nunca recalculada.");

        builder.Property(i => i.NumeroInscricao)
            .HasMaxLength(20)
            .HasComment("Gerado na primeira submissão (sequência do banco, formatada); nulo em rascunho.");

        // Token de concorrência (ADR-0119): a checagem em memória de Inscricao.ConferirPrecondicao
        // devolve 412 limpo quando já basta; isto cobre a corrida que ela sozinha não pega — duas
        // gravações que leram a mesma revisão não passam as duas no UPDATE.
        builder.Property(i => i.Revisao)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);

        // Dois "iniciar" simultâneos do mesmo CPF no mesmo processo: um insere, o outro colide
        // aqui — InscricaoRepository.AdicionarAsync não traduz a violação (sem catch, de propósito,
        // ver o teste de colisão); é o handler de "iniciar" que, ao capturar a exceção, decide
        // devolver a inscrição existente em vez de um 500. Nunca duas linhas para o mesmo candidato.
        builder.HasIndex(i => new { i.ProcessoSeletivoId, i.CpfIndice })
            .IsUnique()
            .HasDatabaseName("ux_inscricoes_processo_cpf_indice");

        // "Minhas inscrições" (issue #1860) busca por CPF em qualquer processo — o índice
        // composto acima não serve essa consulta, porque não filtra por ProcessoSeletivoId.
        builder.HasIndex(i => i.CpfIndice)
            .HasDatabaseName("ix_inscricoes_cpf_indice");
    }
}

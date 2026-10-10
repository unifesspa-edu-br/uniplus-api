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
            "processo. Números de documento vivem cifrados aqui ou em documentos_cifrados, " +
            "nunca em respostas_json."));

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

        builder.Property(i => i.RespostasJson)
            .HasColumnName("respostas")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Respostas por fato (código → valor), sem números de documento.");

        builder.Property(i => i.GruposJson)
            .HasColumnName("grupos")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Ocorrências de grupo repetível, mesma exclusão de documento das respostas.");

        builder.Property(i => i.AceitesJson)
            .HasColumnName("aceites")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Aceites de termo — código, versão e hash da versão fixada.");

        builder.Property(i => i.FatosIdentificadorDeclaradosJson)
            .HasColumnName("fatos_identificador_declarados")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Códigos de fato cuja classificação congelada é IDENTIFICADOR, fixados ao iniciar.");

        builder.Property(i => i.DocumentosCifrados)
            .HasColumnType("bytea")
            .IsRequired()
            .HasComment("Envelope AEAD com o mapa {código do fato: valor} dos números de documento — nunca em claro.");

        // Token de concorrência (ADR-0119): a checagem em memória de Inscricao.ConferirPrecondicao
        // devolve 412 limpo quando já basta; isto cobre a corrida que ela sozinha não pega — duas
        // gravações que leram a mesma revisão não passam as duas no UPDATE.
        builder.Property(i => i.Revisao)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);

        // Dois "iniciar" simultâneos do mesmo CPF no mesmo processo: um insere, o outro colide
        // aqui e recebe a inscrição existente (tradução da violação, no repositório) — nunca um
        // 500 nem duas linhas para o mesmo candidato.
        builder.HasIndex(i => new { i.ProcessoSeletivoId, i.CpfIndice })
            .IsUnique()
            .HasDatabaseName("ux_inscricoes_processo_cpf_indice");
    }
}

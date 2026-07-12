namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core do <see cref="Edital"/> — entidade interna do
/// agregado <see cref="ProcessoSeletivo"/> (Story #759, T4 #785).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EditalConfiguration : IEntityTypeConfiguration<Edital>
{
    private const int NumeroMaxLength = 60;
    private const int MotivoRetificacaoMaxLength = 2000;

    public void Configure(EntityTypeBuilder<Edital> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("editais", ConfigurarChecks);
        builder.HasKey(e => e.Id);
        // Chave Guid v7 gerada no domínio (EntityBase) — mesma convenção do
        // resto do agregado (ver ProcessoSeletivoConfiguration).
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Natureza).HasConversion<int>().IsRequired();
        builder.Property(e => e.Numero).HasMaxLength(NumeroMaxLength);
        builder.Property(e => e.DataPublicacao);
        builder.Property(e => e.DocumentoEditalId).IsRequired();
        builder.Property(e => e.EditalRetificadoId);
        builder.Property(e => e.MotivoRetificacao).HasMaxLength(MotivoRetificacaoMaxLength);

        // FK para o documento (T3, #784) — sem nav property: o Edital não
        // navega para o documento, só carrega a referência congelada.
        builder.HasOne<DocumentoEdital>()
            .WithMany()
            .HasForeignKey(e => e.DocumentoEditalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_editais_documento_edital_id");

        // Auto-referência da cadeia de retificação (ADR-0101) — nulo em
        // Edital de abertura. Restrict: um Edital retificado nunca é
        // removido fisicamente enquanto houver retificação apontando para
        // ele (trilha auditável).
        builder.HasOne<Edital>()
            .WithMany()
            .HasForeignKey(e => e.EditalRetificadoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_editais_edital_retificado_id");

        // Não há unicidade sobre data_publicacao (ADR-0104): a data é DOCUMENTAL
        // — o que o ato declara — e a retificação a republica inalterada. A ordem
        // total da configuração vem de UNIQUE(processo, numero_versao) sobre as
        // versões, não daqui; exigir datas distintas serializava o conjunto
        // errado e recusava dois atos publicados no mesmo instante.

        // Fecha a corrida de duas publicações
        // concorrentes do mesmo processo — só um Edital de abertura por
        // processo, nunca dois, mesmo com duas requisições simultâneas lendo
        // Status=Rascunho antes de qualquer commit.
        builder.HasIndex(e => e.ProcessoSeletivoId)
            .IsUnique()
            .HasFilter($"natureza = {(int)Domain.Enums.NaturezaEdital.Abertura}")
            .HasDatabaseName("ux_editais_processo_abertura_unica");

        // T5 #786 (ADR-0101, cadeia linear): cada Edital é retificado no
        // máximo uma vez — backstop de banco simétrico ao da abertura única.
        // O lock pessimista de ObterComConfiguracaoAsync já serializa
        // retificações do mesmo processo (o guard em memória sempre vê a
        // cadeia atualizada), mas esta trava garante a linearidade como
        // invariante de banco, fechando qualquer janela residual sem que a
        // cadeia possa ramificar.
        builder.HasIndex(e => e.EditalRetificadoId)
            .IsUnique()
            .HasFilter("edital_retificado_id IS NOT NULL")
            .HasDatabaseName("ux_editais_edital_retificado_unico");
    }

    // ADR-0101: contrato abertura×retificação — abertura sem os dois campos
    // de retificação é o único estado válido para essa natureza; retificação
    // exige os dois preenchidos simultaneamente (tudo-ou-nada por natureza).
    // Defesa em profundidade: Edital.EmitirAbertura já garante isso em
    // memória; esta trava cobre gap de validação/concorrência (ADR-0102).
    private static void ConfigurarChecks(TableBuilder<Edital> table)
    {
        int abertura = (int)Domain.Enums.NaturezaEdital.Abertura;
        int retificacao = (int)Domain.Enums.NaturezaEdital.Retificacao;

        table.HasCheckConstraint(
            "ck_editais_contrato_natureza",
            $"(natureza = {abertura} AND edital_retificado_id IS NULL AND motivo_retificacao IS NULL) " +
            $"OR (natureza = {retificacao} AND edital_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL)");
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core dos lotes de importação das notas do ENEM: evidência append-only
/// (<c>IForensicEntity</c>), mapeada direto — nada aqui é cifrado.
/// </summary>
public sealed class LoteDeNotasDoEnemConfiguration : IEntityTypeConfiguration<LoteDeNotasDoEnem>
{
    /// <summary>
    /// A ordem em que os lotes foram registrados, dada pelo banco: desempata dois lotes do mesmo escopo importados no mesmo
    /// microssegundo, que a importação sob a trava do processo serializa. É propriedade sombra porque só a persistência
    /// ordena por ela; o domínio e a Application não a leem.
    /// </summary>
    internal const string OrdemDeRegistro = nameof(OrdemDeRegistro);

    private const int HashHexLength = 64;
    private const int CodigoMaxLength = 60;
    private const int VersaoMaxLength = 20;
    private const int ImportadoPorMaxLength = 255;

    public void Configure(EntityTypeBuilder<LoteDeNotasDoEnem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("importacoes_de_notas_do_enem", t =>
        {
            t.HasComment("Uma importação do retorno do INEP para um processo, uma etapa e uma edição do ENEM. Os registros não são alterados ou excluídos; vale por inteiro o lote mais recente para esses dados. Do arquivo, guarda-se apenas o resumo do conteúdo.");
            t.HasCheckConstraint("ck_importacoes_de_notas_do_enem_hashes", "arquivo_hash ~ '^[0-9a-f]{64}$' AND leiaute_hash ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_importacoes_de_notas_do_enem_totais", "total_de_linhas >= 0 AND total_de_inscritos_com_nota >= 0 AND total_de_lancamentos >= 0 AND total_de_inscritos_sem_linha >= 0 AND total_de_linhas_sem_inscrito >= 0 AND total_de_inscritos_em_conflito >= 0");
            t.HasCheckConstraint("ck_importacoes_de_notas_do_enem_edicao", "edicao_codigo <> ''");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever().HasComment("Identificador da importação.");

        // Alvo da chave estrangeira composta do lançamento: a etapa do lançamento não pode divergir da do lote.
        builder.HasAlternateKey(l => new { l.Id, l.EtapaProcessoId })
            .HasName("ak_importacoes_de_notas_do_enem_id_etapa");

        builder.Property(l => l.ProcessoSeletivoId).IsRequired().HasComment("Identificador do processo seletivo ao qual pertence a importação.");

        builder.Property(l => l.EtapaProcessoId)
            .IsRequired()
            .HasComment("A etapa de nota do ENEM, guardada por valor e sem chave estrangeira: pertence à configuração do processo, que a retificação recria.");

        builder.Property(l => l.EdicaoCodigo)
            .HasMaxLength(CodigoMaxLength)
            .IsRequired()
            .HasComment("O código da opção do fato EDICAO_ENEM, como o processo a declara.");

        builder.Property(l => l.LeiauteCodigo).HasMaxLength(CodigoMaxLength).IsRequired().HasComment("Código do leiaute usado para ler o arquivo.");
        builder.Property(l => l.LeiauteVersao).HasMaxLength(VersaoMaxLength).IsRequired().HasComment("Versão do leiaute usado para ler o arquivo.");

        builder.Property(l => l.LeiauteHash)
            .HasMaxLength(HashHexLength)
            .IsFixedLength()
            .IsRequired()
            .HasComment("SHA-256 da definição do leiaute com que o arquivo foi lido.");

        builder.Property(l => l.ArquivoHash)
            .HasMaxLength(HashHexLength)
            .IsFixedLength()
            .IsRequired()
            .HasComment("SHA-256 dos bytes do arquivo importado.");

        builder.Property(l => l.ImportadoPor).ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasMaxLength(ImportadoPorMaxLength)
            .IsRequired()
            .HasComment("A conta do token de quem importou.");

        builder.Property(l => l.ImportadoPorNome).ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasMaxLength(ImportadoPorMaxLength)
            .HasComment("O nome de quem importou, gravado no momento da ação.");

        builder.Property(l => l.ImportadoEm).IsRequired().HasComment("Data e hora da importação do arquivo.");

        builder.Property<long>(OrdemDeRegistro).UseIdentityAlwaysColumn().ValueGeneratedOnAdd().HasComment("Ordem atribuída pelo banco para desempatar importações do mesmo escopo.");
        builder.HasIndex(OrdemDeRegistro).IsUnique().HasDatabaseName("ux_importacoes_de_notas_do_enem_ordem_de_registro");

        builder.Property(l => l.VersaoConfiguracaoId)
            .IsRequired()
            .HasComment("A versão divulgada do certame cujos pesos apuraram as avaliações do lote.");
        builder.Property(l => l.TotalDeLinhas).IsRequired().HasComment("Quantidade de linhas lidas no arquivo.");
        builder.Property(l => l.TotalDeInscritosComNota).IsRequired().HasComment("Quantidade de inscritos com nota importada.");
        builder.Property(l => l.TotalDeLancamentos).IsRequired().HasComment("Quantidade de notas individuais lançadas.");
        builder.Property(l => l.TotalDeInscritosSemLinha).IsRequired().HasComment("Quantidade de inscritos sem linha correspondente no arquivo.");
        builder.Property(l => l.TotalDeLinhasSemInscrito).IsRequired().HasComment("Quantidade de linhas sem inscrição correspondente.");
        builder.Property(l => l.TotalDeInscritosEmConflito).IsRequired().HasComment("Quantidade de inscritos com linhas conflitantes.");

        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(l => l.ProcessoSeletivoId)
            .HasConstraintName("fk_importacoes_de_notas_do_enem_processo_seletivo")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoConfiguracao>()
            .WithMany()
            .HasForeignKey(l => l.VersaoConfiguracaoId)
            .HasConstraintName("fk_importacoes_de_notas_do_enem_versao_configuracao")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Inscritos)
            .WithOne()
            .HasForeignKey(i => i.LoteId)
            .HasConstraintName("fk_inscritos_da_importacao_importacao")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(l => l.Inscritos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(l => l.Lancamentos)
            .WithOne()
            .HasForeignKey(l => new { l.LoteId, l.EtapaProcessoId })
            .HasConstraintName("fk_lancamentos_de_nota_importacao_de_notas_do_enem")
            .HasPrincipalKey(l => new { l.Id, l.EtapaProcessoId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(l => l.Lancamentos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(l => l.Avaliacoes)
            .WithOne()
            .HasForeignKey(a => new { a.LoteId, a.EtapaProcessoId })
            .HasConstraintName("fk_avaliacoes_da_etapa_importacao_de_notas_do_enem")
            .HasPrincipalKey(l => new { l.Id, l.EtapaProcessoId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(l => l.Avaliacoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(l => l.Ocorrencias)
            .WithOne()
            .HasForeignKey(o => o.LoteId)
            .HasConstraintName("fk_ocorrencias_da_importacao_importacao_de_notas_do_enem")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(l => l.Ocorrencias).UsePropertyAccessMode(PropertyAccessMode.Field);

        // O lote vigente é o primeiro desta ordem; a listagem do escopo usa o mesmo índice.
        builder.HasIndex(
                nameof(LoteDeNotasDoEnem.ProcessoSeletivoId),
                nameof(LoteDeNotasDoEnem.EtapaProcessoId),
                nameof(LoteDeNotasDoEnem.EdicaoCodigo),
                nameof(LoteDeNotasDoEnem.ImportadoEm),
                OrdemDeRegistro)
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("ix_importacoes_de_notas_do_enem_escopo");
    }
}

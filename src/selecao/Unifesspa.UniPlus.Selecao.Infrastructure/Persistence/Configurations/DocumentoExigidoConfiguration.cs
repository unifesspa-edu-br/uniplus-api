namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Domain.Entities;
using Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="DocumentoExigido"/> (Story #554, PR-a) — entidade
/// filha do agregado <see cref="ProcessoSeletivo"/>, <c>EntityBase</c> puro (sem
/// soft-delete). Sem CHECK de banco para <c>aplicabilidade</c>/<c>consequencia_indeferimento</c>
/// — o módulo Seleção só usa esse mecanismo em <c>versoes_configuracao</c> (guard rails
/// forenses da ADR-0102); a integridade é C# (enum tipado + guard de domínio +
/// FluentValidation), mesmo padrão de <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
public sealed class DocumentoExigidoConfiguration : IEntityTypeConfiguration<DocumentoExigido>
{
    private const int TipoDocumentoCodigoMaxLength = 60;
    // Espelha TipoDocumentoConfiguration.NomeMaxLength (Configuracao.Infrastructure) — um
    // snapshot-copy mais curto que a origem trunca silenciosamente (ou falha no Postgres)
    // um cadastro legítimo de até 200 caracteres.
    private const int TipoDocumentoNomeMaxLength = 200;
    private const int TipoDocumentoCategoriaMaxLength = 60;
    private const int ConsequenciaIndeferimentoMaxLength = 30;
    private const int IdadeMaximaUnidadeMaxLength = 10;
    private const int IdadeMaximaReferenciaTipoMaxLength = 20;
    private const int FormatoPermitidoMaxLength = 10;

    public void Configure(EntityTypeBuilder<DocumentoExigido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("documentos_exigidos");
        builder.HasKey(d => d.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // FaseCronograma/ModalidadeSelecionada.
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.ExigidoNaFaseId).IsRequired();
        builder.Property(d => d.TipoDocumentoOrigemId).IsRequired();
        builder.Property(d => d.TipoDocumentoCodigo).HasMaxLength(TipoDocumentoCodigoMaxLength).IsRequired();
        builder.Property(d => d.TipoDocumentoNome).HasMaxLength(TipoDocumentoNomeMaxLength).IsRequired();
        builder.Property(d => d.TipoDocumentoCategoria).HasMaxLength(TipoDocumentoCategoriaMaxLength).IsRequired();
        builder.Property(d => d.Aplicabilidade).HasConversion<int>().IsRequired();
        builder.Property(d => d.Obrigatorio).IsRequired();
        builder.Property(d => d.ConsequenciaIndeferimento).HasMaxLength(ConsequenciaIndeferimentoMaxLength);

        // FK real para fases_cronograma — a checagem "fase viva do mesmo processo"
        // (ProcessoSeletivo.DefinirDocumentosExigidos) é check-then-act não-atômico; a
        // constraint do banco é a defesa realmente atômica (mesmo padrão de
        // FaseCronogramaConfiguration). Restrict, não Cascade: reconfigurar o cronograma
        // (DefinirCronogramaFases) não pode apagar silenciosamente uma fase ainda
        // referenciada por uma exigência viva — falha explícita até a PR-d formalizar o
        // guard de domínio equivalente (CA-04).
        builder.HasOne<FaseCronograma>()
            .WithMany()
            .HasForeignKey(d => d.ExigidoNaFaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ExigidoNaFaseId)
            .HasDatabaseName("ix_documentos_exigidos_exigido_na_fase_id");

        // Gatilho DNF (Story #554, PR-b) — substituível por inteiro junto com o próprio
        // DocumentoExigido, mesmo padrão de FaseCronograma/BancasRequeridas.
        builder.HasMany(d => d.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.DocumentoExigidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Base legal 1:N (Story #554, PR-c) — substituível por inteiro junto com o próprio
        // DocumentoExigido, mesmo padrão de Condicoes.
        builder.HasMany(d => d.BasesLegais)
            .WithOne()
            .HasForeignKey(b => b.DocumentoExigidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.BasesLegais)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Idade máxima de emissão (Story #554, PR-d, issue #893) — VO sem identidade
        // própria, mesmo padrão de ProcessoSeletivo.ReferenciaTemporalFatos (PR-b):
        // OwnsOne, colunas nullable, 0..1 por exigência.
        builder.OwnsOne(d => d.IdadeMaximaEmissao, idade =>
        {
            idade.Property(i => i.Valor).HasColumnName("idade_maxima_valor");
            idade.Property(i => i.Unidade)
                .HasColumnName("idade_maxima_unidade")
                .HasConversion(UnidadeConverter)
                .HasMaxLength(IdadeMaximaUnidadeMaxLength);
            idade.Property(i => i.ReferenciaTipo)
                .HasColumnName("idade_maxima_referencia_tipo")
                .HasConversion(ReferenciaTipoIdadeEmissaoConverter)
                .HasMaxLength(IdadeMaximaReferenciaTipoMaxLength);
            idade.Property(i => i.Data).HasColumnName("idade_maxima_referencia_data");
            idade.Property(i => i.ReferenciaFaseId).HasColumnName("idade_maxima_referencia_fase_id");
        });

        builder.Property(d => d.FormatoPermitido)
            .HasConversion(FormatoPermitidoConverter)
            .HasMaxLength(FormatoPermitidoMaxLength);

        builder.Property(d => d.TamanhoMaximoBytes);
    }

    private static readonly ValueConverter<UnidadeIdade, string?> UnidadeConverter =
        new(
            unidade => unidade == UnidadeIdade.Nenhuma ? null : unidade.ToCodigo(),
            codigo => UnidadeIdadeCodigo.FromCodigo(codigo) ?? UnidadeIdade.Nenhuma);

    private static readonly ValueConverter<ReferenciaTipoIdadeEmissao, string?> ReferenciaTipoIdadeEmissaoConverter =
        new(
            tipo => tipo == ReferenciaTipoIdadeEmissao.Nenhuma ? null : tipo.ToCodigo(),
            codigo => ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(codigo) ?? ReferenciaTipoIdadeEmissao.Nenhuma);

    private static readonly ValueConverter<FormatoPermitido?, string?> FormatoPermitidoConverter =
        new(
            formato => formato == null || formato.Value == FormatoPermitido.Nenhum ? null : formato.Value.ToCodigo(),
            codigo => FormatoPermitidoCodigo.FromCodigo(codigo));
}

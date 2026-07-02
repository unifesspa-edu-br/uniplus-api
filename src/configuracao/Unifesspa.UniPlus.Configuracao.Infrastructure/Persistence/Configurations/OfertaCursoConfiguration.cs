namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class OfertaCursoConfiguration : IEntityTypeConfiguration<OfertaCurso>
{
    private const int EnumTokenMaxLength = 30;
    private const int EMecCodigoMaxLength = 20;
    private const int CodigoSgaMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AtoAutorizacaoMecMaxLength = 300;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<OfertaCurso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("oferta_curso", ConfigurarChecks);

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CursoId).IsRequired();
        builder.Property(o => o.LocalOfertaId).IsRequired();

        builder.Property(o => o.ProgramaDeOferta)
            .HasConversion<ProgramaDeOfertaValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(o => o.FormatoPedagogico)
            .HasConversion<FormatoPedagogicoValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        // Enum nullable: o converter cobre o valor não-nulo; o EF encapsula null.
        builder.Property(o => o.Turno)
            .HasConversion(new TurnoOfertaValueConverter())
            .HasMaxLength(EnumTokenMaxLength);

        // Nome explícito: a convenção snake_case quebraria "EMecCodigo" de forma
        // não óbvia — fixa e_mec_codigo como contrato de schema.
        builder.Property(o => o.EMecCodigo)
            .HasColumnName("e_mec_codigo")
            .HasMaxLength(EMecCodigoMaxLength);

        builder.Property(o => o.CodigoSga).HasMaxLength(CodigoSgaMaxLength);
        builder.Property(o => o.VagasAnuaisAutorizadas);
        builder.Property(o => o.BaseLegal).HasMaxLength(BaseLegalMaxLength);
        builder.Property(o => o.AtoAutorizacaoMec).HasMaxLength(AtoAutorizacaoMecMaxLength);

        // Snapshot-copy da unidade ofertante (ADR-0061): owned type obrigatório,
        // table splitting em colunas unidade_oft_* — todas NOT NULL, SEM FK para
        // Organização (a proveniência é só o origem_id).
        builder.OwnsOne(o => o.UnidadeOfertante, unidade =>
        {
            unidade.Property(u => u.OrigemId)
                .HasColumnName("unidade_oft_origem_id")
                .IsRequired();
            unidade.Property(u => u.Sigla)
                .HasColumnName("unidade_oft_sigla")
                .HasMaxLength(UnidadeOfertante.SiglaMaxLength)
                .IsRequired();
            unidade.Property(u => u.Nome)
                .HasColumnName("unidade_oft_nome")
                .HasMaxLength(UnidadeOfertante.NomeMaxLength)
                .IsRequired();
            unidade.Property(u => u.Tipo)
                .HasColumnName("unidade_oft_tipo")
                .HasMaxLength(UnidadeOfertante.TipoMaxLength)
                .IsRequired();
        });
        builder.Navigation(o => o.UnidadeOfertante).IsRequired();

        // Auditoria (IAuditableEntity)
        builder.Property(o => o.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(o => o.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // FKs intra-schema com RESTRICT: a remoção lógica de Curso/LocalOferta é
        // barrada pelos handlers (RemocaoBloqueadaPorOfertaCurso, via
        // ReferenciadoPorOfertaCursoVivaAsync); o RESTRICT cobre o DELETE físico
        // residual — mesmo expediente do local_oferta → campus.
        builder.HasOne<Curso>()
            .WithMany()
            .HasForeignKey(o => o.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LocalOferta>()
            .WithMany()
            .HasForeignKey(o => o.LocalOfertaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.CursoId)
            .HasDatabaseName("ix_oferta_curso_curso_id");

        builder.HasIndex(o => o.LocalOfertaId)
            .HasDatabaseName("ix_oferta_curso_local_oferta_id");
    }

    private static void ConfigurarChecks(TableBuilder<OfertaCurso> table)
    {
        // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
        table.HasCheckConstraint(
            "ck_oferta_curso_programa_de_oferta",
            $"programa_de_oferta IN ({TokensSql(ProgramasDeOferta.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_oferta_curso_formato_pedagogico",
            $"formato_pedagogico IN ({TokensSql(FormatosPedagogicos.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_oferta_curso_turno",
            $"turno IS NULL OR turno IN ({TokensSql(TurnosOferta.TokensCanonicos)})");

        // Teto e-MEC: nulo aceito; zero aceito; negativo nunca.
        table.HasCheckConstraint(
            "ck_oferta_curso_vagas_anuais_autorizadas",
            "vagas_anuais_autorizadas IS NULL OR vagas_anuais_autorizadas >= 0");

        // Guard condicional da base legal (ADR-0066): programa fora do Regular
        // exige base legal — espelha no banco o guard de Criar/Atualizar.
        table.HasCheckConstraint(
            "ck_oferta_curso_base_legal_programa",
            "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));
}

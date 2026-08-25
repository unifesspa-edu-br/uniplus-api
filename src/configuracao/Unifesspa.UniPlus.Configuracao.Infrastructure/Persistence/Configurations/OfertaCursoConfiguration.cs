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

        builder.Property(o => o.RegimeDeTurno)
            .HasConversion<RegimeDeTurnoValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        // Coleção primitiva de 1..2 tokens de domínio fechado, mapeada a um array
        // nativo do Postgres (varchar(30)[]). Array em vez de tabela filha porque
        // só ele permite ao banco recusar, numa única expressão sem subquery, as
        // três formas de violação da cardinalidade: lista vazia, turno repetido e
        // quantidade incompatível com o regime declarado (ver ConfigurarChecks).
        // Uma tabela filha com PK composta recusaria apenas a repetição.
        builder.PrimitiveCollection(o => o.Turnos)
            .HasColumnName("turnos")
            .ElementType(elemento => elemento
                .HasConversion<TurnoOfertaValueConverter>()
                .HasMaxLength(EnumTokenMaxLength))
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

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
            "ck_oferta_curso_regime_de_turno",
            $"regime_de_turno IN ({TokensSql(RegimesDeTurno.TokensCanonicos)})");

        // Domínio fechado de cada elemento do array (o `<@` compara conjuntos).
        table.HasCheckConstraint(
            "ck_oferta_curso_turnos_dominio",
            $"turnos::text[] <@ ARRAY[{TokensSql(TurnosOferta.TokensCanonicos)}]::text[]");

        // Cardinalidade e distinção conforme o regime declarado (UNI-REQ-0137):
        // REGULAR ocupa exatamente um turno; INTEGRAL, exatamente dois distintos.
        // Espelha no banco a invariante de Criar/Atualizar.
        //
        // A comparação por subscrito (turnos[1] <> turnos[2]) só é confiável sobre
        // um array unidimensional de limite inferior 1: o Postgres aceita arrays
        // multidimensionais e com limite inferior arbitrário, e um subscrito fora
        // da faixa devolve NULL — que num CHECK conta como satisfeito. Daí as duas
        // guardas de forma antes da regra. O `coalesce` cobre o array vazio, cujos
        // array_ndims/array_lower são NULL.
        table.HasCheckConstraint(
            "ck_oferta_curso_turnos_regime",
            "coalesce(array_ndims(turnos), 0) = 1 "
            + "AND coalesce(array_lower(turnos, 1), 1) = 1 "
            + "AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) "
            + "OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))");

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

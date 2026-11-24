namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class OfertaCursoConfiguration : IEntityTypeConfiguration<OfertaCurso>
{
    private const int CodigoDeContratoMaxLength = 30;
    private const int CodigoMaxLength = CodigoOfertaCurso.TamanhoMaximo;
    private const int EMecCodigoMaxLength = 20;
    private const int CodigoSgaMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AtoAutorizacaoMecMaxLength = 300;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<OfertaCurso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("ofertas_de_curso", ConfigurarChecks);
        builder.HasKey(o => o.Id);
        ConfigurarIdentificacao(builder);
        ConfigurarClassificacoes(builder);
        ConfigurarRegimes(builder);
        ConfigurarTurnos(builder);
        ConfigurarCadastroNoMec(builder);
        ConfigurarUnidadeOfertante(builder);
        ConfigurarAuditoria(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarIndices(builder);
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.Property(o => o.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(o => o.CursoId).IsRequired();
        builder.Property(o => o.LocalOfertaId).IsRequired();
    }

    private static void ConfigurarClassificacoes(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.Property(o => o.ProgramaDeOferta)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();

        builder.Property(o => o.FormatoPedagogico)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();
    }

    private static void ConfigurarRegimes(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.Property(o => o.RegimeDeFuncionamento)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();

        builder.Property(o => o.RegimeDeTurno)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();
    }

    // Coleção primitiva de 1..2 códigos de domínio fechado, mapeada a um array
    // nativo do Postgres (varchar(30)[]). Array em vez de tabela filha porque
    // só ele permite ao banco recusar, numa única expressão sem subquery, as
    // três formas de violação da cardinalidade: lista vazia, turno repetido e
    // quantidade incompatível com o regime declarado (ver ConfigurarChecksDosTurnos).
    // Uma tabela filha com PK composta recusaria apenas a repetição.
    private static void ConfigurarTurnos(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.PrimitiveCollection(o => o.Turnos)
            .HasColumnName("turnos")
            .ElementType(elemento => elemento.HasMaxLength(CodigoDeContratoMaxLength))
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }

    // Os nomes das colunas identificam os códigos nos respectivos sistemas.
    private static void ConfigurarCadastroNoMec(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.Property(o => o.EMecCodigo)
            .HasColumnName("codigo_emec")
            .HasMaxLength(EMecCodigoMaxLength);

        builder.Property(o => o.CodigoSga).HasColumnName("codigo_no_sistema_academico").HasMaxLength(CodigoSgaMaxLength);
        builder.Property(o => o.VagasAnuaisAutorizadas);
        builder.Property(o => o.BaseLegal).HasMaxLength(BaseLegalMaxLength);
        builder.Property(o => o.AtoAutorizacaoMec).HasMaxLength(AtoAutorizacaoMecMaxLength);
    }

    // Cópia dos dados da unidade ofertante (ADR-0061): owned type obrigatório,
    // table splitting em colunas unidade_ofertante_* — todas NOT NULL, SEM FK para
    // Organização (a proveniência é só o origem_id).
    private static void ConfigurarUnidadeOfertante(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.OwnsOne(o => o.UnidadeOfertante, ConfigurarColunasDaUnidadeOfertante);
        builder.Navigation(o => o.UnidadeOfertante).IsRequired();
    }

    private static void ConfigurarColunasDaUnidadeOfertante(OwnedNavigationBuilder<OfertaCurso, UnidadeOfertante> unidade)
    {
        unidade.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        unidade.Property(u => u.OrigemId).HasColumnName("unidade_ofertante_origem_id").IsRequired();
        unidade.Property(u => u.Sigla)
            .HasColumnName("unidade_ofertante_sigla").HasMaxLength(UnidadeOfertante.SiglaMaxLength).IsRequired();
        unidade.Property(u => u.Nome)
            .HasColumnName("unidade_ofertante_nome").HasMaxLength(UnidadeOfertante.NomeMaxLength).IsRequired();
        unidade.Property(u => u.Tipo)
            .HasColumnName("unidade_ofertante_tipo").HasMaxLength(UnidadeOfertante.TipoMaxLength).IsRequired();
    }

    // Auditoria (IAuditableEntity)
    private static void ConfigurarAuditoria(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.Property(o => o.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(o => o.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    // FKs intra-schema com RESTRICT: a remoção lógica de Curso/LocalOferta é
    // barrada pelos handlers (RemocaoBloqueadaPorOfertaCurso, via
    // ReferenciadoPorOfertaCursoVivaAsync); o RESTRICT cobre o DELETE físico
    // residual — mesmo expediente do locais_de_oferta → campus.
    private static void ConfigurarRelacionamentos(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.HasOne<Curso>()
            .WithMany()
            .HasForeignKey(o => o.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LocalOferta>()
            .WithMany()
            .HasForeignKey(o => o.LocalOfertaId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // O código identifica a oferta viva: oferta removida libera o valor, e o índice decide a corrida entre dois cadastros.
    // O Id entra como segunda coluna do índice por curso porque a listagem restrita a um curso
    // ordena pelo nome do curso — constante dentro da listagem —, deixando o
    // Id como única coluna que move o seek de página em página.
    private static void ConfigurarIndices(EntityTypeBuilder<OfertaCurso> builder)
    {
        builder.HasIndex(o => o.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.OfertaDeCursoCodigoAtivos);

        builder.HasIndex(o => new { o.CursoId, o.Id })
            .HasDatabaseName("ix_ofertas_de_curso_curso_id");

        builder.HasIndex(o => o.LocalOfertaId)
            .HasDatabaseName("ix_ofertas_de_curso_local_oferta_id");
    }

    private static void ConfigurarChecks(TableBuilder<OfertaCurso> table)
    {
        ConfigurarChecksDoCadastro(table);
        ConfigurarChecksDoPrograma(table);
        ConfigurarChecksDosDominiosDosRegimes(table);
        ConfigurarChecksDosTurnos(table);
        ConfigurarChecksDeCoerenciaEntreRegimes(table);
    }

    // Formato do código (defesa em profundidade contra insert cru): o mesmo que o objeto de valor confere.
    // Teto e-MEC: nulo aceito; zero aceito; negativo nunca. Domínio fechado do formato pedagógico.
    private static void ConfigurarChecksDoCadastro(TableBuilder<OfertaCurso> table)
    {
        table.HasCheckConstraint(
            "ck_ofertas_de_curso_codigo_formato",
            "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");

        table.HasCheckConstraint(
            "ck_ofertas_de_curso_vagas_anuais_autorizadas",
            "vagas_anuais_autorizadas IS NULL OR vagas_anuais_autorizadas >= 0");

        table.HasCheckConstraint(
            "ck_ofertas_de_curso_formato_pedagogico",
            $"formato_pedagogico IN ({SqlDosCodigosDeContrato.Lista<FormatoPedagogico>()})");
    }

    // Domínio fechado do programa de oferta e guard condicional da base legal (ADR-0066): programa
    // fora do Regular exige base legal — espelha no banco o guard de Criar/Atualizar.
    private static void ConfigurarChecksDoPrograma(TableBuilder<OfertaCurso> table)
    {
        table.HasCheckConstraint(
            "ck_ofertas_de_curso_programa_de_oferta",
            $"programa_de_oferta IN ({SqlDosCodigosDeContrato.Lista<ProgramaDeOferta>()})");

        table.HasCheckConstraint(
            "ck_ofertas_de_curso_base_legal_programa",
            $"programa_de_oferta = {SqlDosCodigosDeContrato.Literal(ProgramaDeOferta.Regular)} OR base_legal IS NOT NULL");
    }

    // Domínios fechados dos regimes e de cada elemento do array de turnos (o `<@` compara conjuntos).
    private static void ConfigurarChecksDosDominiosDosRegimes(TableBuilder<OfertaCurso> table)
    {
        table.HasCheckConstraint(
            "ck_ofertas_de_curso_regime_de_turno",
            $"regime_de_turno IN ({SqlDosCodigosDeContrato.Lista<RegimeDeTurno>()})");

        table.HasCheckConstraint(
            "ck_ofertas_de_curso_regime_de_funcionamento",
            $"regime_de_funcionamento IN ({SqlDosCodigosDeContrato.Lista<RegimeDeFuncionamento>()})");

        table.HasCheckConstraint(
            "ck_ofertas_de_curso_turnos_dominio",
            $"turnos::text[] <@ ARRAY[{SqlDosCodigosDeContrato.Lista<TurnoOferta>()}]::text[]");
    }

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
    private static void ConfigurarChecksDosTurnos(TableBuilder<OfertaCurso> table)
    {
        table.HasCheckConstraint("ck_ofertas_de_curso_turnos_regime", CardinalidadeDosTurnosSql());
    }

    private static string CardinalidadeDosTurnosSql()
    {
        string regular = SqlDosCodigosDeContrato.Literal(RegimeDeTurno.Regular);
        string integral = SqlDosCodigosDeContrato.Literal(RegimeDeTurno.Integral);
        int turnosDoRegular = RegimesDeTurno.TurnosExigidos(RegimeDeTurno.Regular);
        int turnosDoIntegral = RegimesDeTurno.TurnosExigidos(RegimeDeTurno.Integral);

        return "coalesce(array_ndims(turnos), 0) = 1 "
            + "AND coalesce(array_lower(turnos, 1), 1) = 1 "
            + $"AND ((regime_de_turno = {regular} AND cardinality(turnos) = {turnosDoRegular}) "
            + $"OR (regime_de_turno = {integral} AND cardinality(turnos) = {turnosDoIntegral} AND turnos[1] <> turnos[2]))";
    }

    // Compatibilidade entre as duas dimensões (UNI-REQ-0138): a oferta
    // INTENSIVA exige regime de turno INTEGRAL; a EXTENSIVA aceita ambos.
    // Espelha no banco a invariante de Criar/Atualizar. A expressão é derivada
    // do mesmo vocabulário que o agregado consulta (RegimeDeTurnoExigido), para o
    // CHECK não divergir da regra caso o vocabulário cresça.
    private static void ConfigurarChecksDeCoerenciaEntreRegimes(TableBuilder<OfertaCurso> table)
    {
        table.HasCheckConstraint(
            "ck_ofertas_de_curso_funcionamento_regime_de_turno",
            CompatibilidadeFuncionamentoRegimeSql());
    }

    // Uma cláusula por regime de funcionamento que restringe o regime de turno;
    // os que não restringem (EXTENSIVO e ALTERNANCIA_PEDAGOGICA) não entram na expressão. Com o
    // vocabulário atual resulta em
    // `(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')`.
    private static string CompatibilidadeFuncionamentoRegimeSql()
    {
        IEnumerable<string> clausulas = CatalogoDe<RegimeDeFuncionamento>.Valores()
            .Select(funcionamento => (Funcionamento: funcionamento, Exigido: RegimesDeFuncionamento.RegimeDeTurnoExigido(funcionamento)))
            .Where(par => par.Exigido is not null)
            .Select(par =>
                $"(regime_de_funcionamento <> {SqlDosCodigosDeContrato.Literal(par.Funcionamento)} "
                + $"OR regime_de_turno = {SqlDosCodigosDeContrato.Literal(par.Exigido!.Value)})");

        return string.Join(" AND ", clausulas);
    }
}

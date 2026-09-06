namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CursoConfiguration
    : IEntityTypeConfiguration<Curso>
{
    /// <summary>
    /// Nome da propriedade sombra que carrega a chave de ordenação alfabética do
    /// curso. Os repositórios que ordenam a listagem a projetam por
    /// <c>EF.Property&lt;string&gt;</c>.
    /// </summary>
    internal const string NomeOrdenacaoPropriedade = "NomeOrdenacao";

    /// <summary>
    /// Expressão da coluna gerada, vinda da normalização compartilhada: a mesma
    /// regra que a aplicação aplica ao termo pesquisado, para que a comparação
    /// entre os dois seja possível.
    /// </summary>
    private static readonly string NomeOrdenacaoSql = NormalizacaoTextual.ExpressaoSql("nome");

    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int GrauMaxLength = 60;
    private const int NivelEnsinoMaxLength = 60;
    private const int GrupoAreaEnemMaxLength = 30;

    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "curso",
            t =>
            {
                // Domínio fechado do grupo de área do ENEM (Res. 805/2024, Anexo I) —
                // espelha o CHECK de peso_area_enem, mas null-safe: a coluna é opcional
                // (nem todo curso classifica por área do ENEM).
                t.HasCheckConstraint(
                    "ck_curso_grupo_area_enem",
                    "grupo_area_enem IS NULL OR grupo_area_enem IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
            });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Grau).HasMaxLength(GrauMaxLength).IsRequired();
        builder.Property(c => c.NivelEnsino).HasMaxLength(NivelEnsinoMaxLength).IsRequired();

        // GrupoAreaEnem é value object opcional — persistido por valor como varchar
        // via GrupoCursoValueConverter (reidratação fail-fast; o converter só é
        // aplicado a valores não-nulos). O CHECK acima restringe a coluna ao
        // domínio fechado. O nome de coluna snake_case vem da convenção global.
        builder.Property(c => c.GrupoAreaEnem)
            .HasConversion<GrupoCursoValueConverter>()
            .HasMaxLength(GrupoAreaEnemMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre cursos vivos (índice parcial) — um curso vivo
        // por código; soft-delete libera o slot para recriação.
        builder.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_curso_codigo_vivo");

        ConfigurarOrdenacaoAlfabetica(builder);
    }

    /// <summary>
    /// Chave de ordenação alfabética do curso: coluna gerada pelo banco a partir
    /// do nome, sem acento e em minúsculas, com collation <c>C</c>. É propriedade
    /// sombra — pertence à ordenação da listagem, não ao domínio nem ao contrato
    /// wire; os repositórios a leem por projeção.
    /// </summary>
    /// <remarks>
    /// <para>Sem essa coluna a ordem sai errada em qualquer instalação cuja
    /// collation ordene por ponto de código: "Zoologia" precederia "biologia" e
    /// "Álgebra". Normalizar antes de comparar tira acento e caixa da decisão de
    /// ordem, que é o que "ordem alfabética" significa para quem lê a listagem.</para>
    /// <para>A collation fixa em <c>C</c> torna a comparação uma ordem de bytes
    /// sobre texto já reduzido a ASCII — mesmo resultado em qualquer servidor,
    /// independentemente do locale com que o banco foi criado. A normalização
    /// Unicode para a forma composta faz o acento decomposto (letra + diacrítico
    /// combinante) ser reconhecido pela substituição.</para>
    /// <para>Coluna gerada não tem caminho de escrita próprio: não há como
    /// dessincronizá-la do nome. A regra de chave de ordenação não nula
    /// (ADR-0095) é satisfeita porque <c>nome</c> é obrigatório.</para>
    /// </remarks>
    private static void ConfigurarOrdenacaoAlfabetica(EntityTypeBuilder<Curso> builder)
    {
        builder.Property<string>(NomeOrdenacaoPropriedade)
            .HasMaxLength(NomeMaxLength)
            .UseCollation("C")
            .HasComputedColumnSql(NomeOrdenacaoSql, stored: true)
            .IsRequired();

        // Casa exatamente o ORDER BY da listagem alfabética (nome normalizado,
        // código, id) e o filtro global de soft-delete aplicado a toda leitura.
        builder.HasIndex(NomeOrdenacaoPropriedade, nameof(Curso.Codigo), nameof(Curso.Id))
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_curso_ordenacao_alfabetica");
    }
}

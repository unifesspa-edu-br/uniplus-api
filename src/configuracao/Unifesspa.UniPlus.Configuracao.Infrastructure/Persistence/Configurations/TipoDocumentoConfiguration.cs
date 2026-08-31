namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoDocumentoConfiguration
    : IEntityTypeConfiguration<TipoDocumento>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    // Acompanha o teto do código no cadastro de categorias (2 a 50): dimensionar
    // por baixo faria uma categoria legítima estourar o banco em vez de ser aceita.
    private const int CategoriaMaxLength = 50;
    private const int FormatosAceitosMaxLength = 200;
    // Acompanha o teto do próprio código: o campo guarda o código de outro tipo.
    private const int TipoEquivalenteMaxLength = 50;

    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "tipo_documento",
            t =>
            {
                // Forma do código da categoria (UPPER_SNAKE iniciando por letra) — defesa
                // em profundidade contra insert cru. Restringe a forma, não o conjunto:
                // qualquer categoria que o cadastro aceite passa aqui, inclusive as que
                // ele venha a ganhar, e a que ele perder continua válida na linha antiga.
                t.HasCheckConstraint(
                    "ck_tipo_documento_categoria_formato",
                    "categoria ~ '^[A-Z][A-Z0-9_]{1,49}$'");

                // Forma do próprio código — defesa em profundidade do invariante de
                // domínio (CodigoTipoDocumento) contra insert cru. Sem ele, um valor
                // gravado por fora só apareceria como falha de reidratação, e aí toda
                // leitura da tabela estouraria por causa de uma única linha.
                t.HasCheckConstraint(
                    "ck_tipo_documento_codigo_formato",
                    "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");

                // Tipo equivalente é rótulo classificatório: nunca aponta para o próprio
                // código. Null-safe (a coluna é opcional). Case-sensitive, alinhado ao
                // guard de domínio (StringComparison.Ordinal).
                t.HasCheckConstraint(
                    "ck_tipo_documento_equivalente_diferente_codigo",
                    "tipo_equivalente IS NULL OR tipo_equivalente <> codigo");

                // Tamanho máximo (quando informado) é positivo — defesa em profundidade
                // do invariante de domínio contra inserts crus (espelha a proteção
                // numérica de peso_area_enem). Null-safe (a coluna é opcional).
                t.HasCheckConstraint(
                    "ck_tipo_documento_tamanho_maximo_mb_positivo",
                    "tamanho_maximo_mb IS NULL OR tamanho_maximo_mb > 0");
            });

        builder.HasKey(t => t.Id);

        // Codigo é value object — persistido por valor como varchar via
        // CodigoTipoDocumentoValueConverter (reidratação fail-fast).
        builder.Property(t => t.Codigo)
            .HasConversion<CodigoTipoDocumentoValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.Descricao).HasMaxLength(DescricaoMaxLength);

        // Categoria é o código de uma CategoriaDocumento do cadastro, guardado como
        // texto sem chave estrangeira — referência classificatória, no molde de
        // PrecedenciaFase → FaseCanonica. Sem CHECK de domínio: o vocabulário deixou
        // de ser fechado em código, e quem responde pela existência é o handler.
        // O teto acompanha o do código no cadastro (CodigoCategoriaDocumento).
        builder.Property(t => t.Categoria)
            .HasMaxLength(CategoriaMaxLength)
            .IsRequired();

        builder.Property(t => t.FormatosAceitos).HasMaxLength(FormatosAceitosMaxLength);
        builder.Property(t => t.TamanhoMaximoMb);
        builder.Property(t => t.TipoEquivalente).HasMaxLength(TipoEquivalenteMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(t => t.CreatedBy).HasMaxLength(255);
        builder.Property(t => t.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre tipos vivos (índice parcial) — um tipo vivo por
        // código; soft-delete libera o slot para recriação.
        builder.HasIndex(t => t.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_documento_codigo_vivo");

        // Índice de filtro por categoria na interface administrativa.
        builder.HasIndex(t => t.Categoria)
            .HasDatabaseName("ix_tipo_documento_categoria");

        builder.HasData(MaterializarSeed());
    }

    /// <summary>
    /// Projeta o seed (<see cref="TipoDocumentoSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration. O instante-âncora é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>/<c>SoftDeleteInterceptor</c>);
    /// qualquer mudança futura no seed exige uma nova migration — o EF detecta o
    /// diff —, sem alterar as bases já migradas.
    /// </summary>
    /// <remarks>
    /// O cadastro é administrado e semeado ao mesmo tempo, então as duas escritas
    /// disputam as mesmas linhas: editar um item desta lista faz o EF gerar um
    /// <c>UpdateData</c> que sobrescreve o que o administrador tiver mudado no tipo
    /// semeado. É o mesmo trade-off já aceito em <c>CategoriaDocumentoConfiguration</c>
    /// e <c>PrecedenciaFaseConfiguration</c>, e o que faz o catálogo existir desde a
    /// migração, sem ato operacional pós-deploy.
    /// </remarks>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante-âncora fixo do seed (HasData exige valor determinístico).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return TipoDocumentoSeed.Itens.Select(item => new
        {
            item.Id,
            Codigo = CodigoTipoDocumento.Criar(item.Codigo).Value!,
            item.Nome,
            Descricao = (string?)null,
            item.Categoria,
            FormatosAceitos = (string?)null,
            TamanhoMaximoMb = (int?)null,
            TipoEquivalente = (string?)null,
            CreatedAt = seedCriadoEm,
            IsDeleted = false,
        });
    }
}

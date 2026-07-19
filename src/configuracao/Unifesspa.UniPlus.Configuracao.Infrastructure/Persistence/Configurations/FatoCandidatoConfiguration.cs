namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Configuração EF Core do catálogo <see cref="FatoCandidato"/> — a tabela
/// <c>rol_de_fatos_candidato</c> (UNI-REQ-0077, ADR-0111).
/// </summary>
/// <remarks>
/// <para>
/// A tabela é <strong>seed-governada e append-only</strong>: não há CRUD de
/// administrador, a única escrita é o seed, e a entidade deriva de
/// <c>EntityBase</c> puro (sem soft-delete). Por isso o índice único
/// <c>ux_rol_de_fatos_candidato_codigo</c> é <strong>total</strong> (não parcial, como o
/// dos cadastros editáveis) — o código é chave natural imutável de uma linha que
/// nunca é logicamente removida. O append-only é imposto por convenção (ausência
/// de API de mutação; leitura via <c>IFatoCandidatoReader</c>), não por gatilho.
/// </para>
/// <para>
/// <c>dominio</c>, <c>origem</c> e <c>cardinalidade</c> são enums persistidos
/// como token canônico UPPER_SNAKE por value converter (reidratação fail-fast); um
/// CHECK por coluna restringe o texto ao domínio fechado. <c>valores_dominio</c> é
/// <c>jsonb</c> <strong>anulável</strong>: o nulo é significante (categórico de
/// escopo-processo, ou booleano/numérico) e é preservado no round-trip — não há
/// default <c>'[]'</c> que o mascare. Um CHECK garante a coerência do preenchimento
/// com o domínio. <c>ponto_resolucao</c>/<c>binding</c> (ADR-0116) são referência
/// por valor, sem FK.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class FatoCandidatoConfiguration : IEntityTypeConfiguration<FatoCandidato>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int EnumTokenMaxLength = 20;
    private const int PontoResolucaoMaxLength = 50;
    private const int BindingMaxLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public void Configure(EntityTypeBuilder<FatoCandidato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rol_de_fatos_candidato", ConfigurarChecks);

        builder.HasKey(f => f.Id);

        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever força o
        // EF a tratar a chave como fornecida pela aplicação (convenção do repo).
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(f => f.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(f => f.Descricao).HasMaxLength(DescricaoMaxLength);

        builder.Property(f => f.Dominio)
            .HasConversion(DominioConverter)
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(f => f.Origem)
            .HasConversion(OrigemConverter)
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(f => f.Cardinalidade)
            .HasConversion(CardinalidadeConverter)
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        // valores_dominio: conjunto fechado de um categórico estático, serializado
        // como jsonb anulável. Sem default '[]' — o nulo (escopo-processo, ou
        // booleano/numérico) é significante e precisa sobreviver ao round-trip. O
        // converter não-nulo é passado pelo overload não-genérico (a coluna
        // anulável é encapsulada pelo EF: null → NULL, sem chamar o converter).
        builder.Property(f => f.ValoresDominio)
            .HasConversion((ValueConverter)ValoresDominioConverter, ValoresDominioComparer)
            .HasColumnType("jsonb");

        // ponto_resolucao/binding (ADR-0116): referência por valor, sem FK — mesmo
        // padrão de dominio/origem/cardinalidade (código como valor, não linha viva).
        builder.Property(f => f.PontoResolucao).HasMaxLength(PontoResolucaoMaxLength).IsRequired();
        builder.Property(f => f.Binding).HasMaxLength(BindingMaxLength).IsRequired();

        // Coleção filha (ADR-0116): descrição por valor de um categórico estático.
        // Cascade porque o filho não tem sentido sem o pai (mesmo padrão de
        // OfertaAtendimentoEspecializado.Condicoes).
        builder.HasMany(f => f.ValoresDominioDeclarados)
            .WithOne()
            .HasForeignKey(v => v.FatoCandidatoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.ValoresDominioDeclarados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // UNIQUE total do código (chave natural imutável): o catálogo é append-only
        // e sem soft-delete, então não há slot a liberar — a unicidade é absoluta.
        builder.HasIndex(f => f.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_rol_de_fatos_candidato_codigo");

        builder.HasData(MaterializarSeed());
    }

    private static void ConfigurarChecks(TableBuilder<FatoCandidato> table)
    {
        // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
        table.HasCheckConstraint(
            "ck_rol_de_fatos_candidato_dominio",
            $"dominio IN ({TokensSql(DominiosFato.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_rol_de_fatos_candidato_origem",
            $"origem IN ({TokensSql(OrigensFato.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_rol_de_fatos_candidato_cardinalidade",
            $"cardinalidade IN ({TokensSql(CardinalidadesFato.TokensCanonicos)})");

        // Coerência valores_dominio × domínio (invariante da factory, replicada no
        // banco): só categórico pode enumerar valores; quando enumera, é um array
        // jsonb não vazio cujos elementos são todos strings não vazias (o jsonpath
        // rejeita elemento de outro tipo, ex.: [1], e string em branco ASCII, ex.:
        // ["  "] — que a factory recusa e que quebraria a desserialização do reader).
        // Nulo é sempre válido (escopo-processo / não-categórico). O regex \s do
        // jsonpath do Postgres cobre o whitespace ASCII; a paridade total com o
        // char.IsWhiteSpace da factory (whitespace Unicode como NBSP) não é
        // exprimível no like_regex (não há \p{Z}), então a factory permanece o guarda
        // autoritativo — o CHECK é defesa em profundidade para os casos realistas de
        // insert cru, e o seed jamais produz esses valores. A ausência de duplicatas,
        // idem: garantida pela factory e pelo teste do seed.
        table.HasCheckConstraint(
            "ck_rol_de_fatos_candidato_valores_dominio_coerente",
            """
            valores_dominio IS NULL OR (
                dominio = 'CATEGORICO'
                AND jsonb_typeof(valores_dominio) = 'array'
                AND valores_dominio <> '[]'::jsonb
                AND NOT (valores_dominio @? '$[*] ? (@.type() != "string" || @ like_regex "^\\s*$")')
            )
            """);
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));

    /// <summary>
    /// Projeta o seed (<see cref="FatoCandidatoSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration. O instante-âncora é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>); qualquer mudança
    /// futura no seed exige uma nova migration (o EF detecta o diff), sem alterar
    /// as bases já migradas.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante-âncora fixo do seed (HasData exige valor determinístico).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return FatoCandidatoSeed.Itens.Select(item => new
        {
            item.Id,
            item.Codigo,
            item.Nome,
            item.Descricao,
            item.Dominio,
            item.Origem,
            item.Cardinalidade,
            item.ValoresDominio,
            item.PontoResolucao,
            item.Binding,
            CreatedAt = seedCriadoEm,
        });
    }

    // ── Conversores/comparadores ──────────────────────────────────────────────

    private static readonly ValueConverter<DominioFato, string> DominioConverter =
        new(dominio => DominiosFato.ParaTokenCanonico(dominio), token => DominiosFato.Analisar(token));

    private static readonly ValueConverter<OrigemFato, string> OrigemConverter =
        new(origem => OrigensFato.ParaTokenCanonico(origem), token => OrigensFato.Analisar(token));

    private static readonly ValueConverter<CardinalidadeFato, string> CardinalidadeConverter =
        new(cardinalidade => CardinalidadesFato.ParaTokenCanonico(cardinalidade), token => CardinalidadesFato.Analisar(token));

    // Lista de códigos serializada como jsonb. Nulo é encapsulado pelo EF (o
    // converter cobre apenas o valor não-nulo) e persiste como NULL na coluna.
    private static readonly ValueConverter<IReadOnlyList<string>, string> ValoresDominioConverter =
        new(
            valores => JsonSerializer.Serialize(valores, JsonOptions),
            json => DeserializarValores(json));

    // Null-aware: o nulo (escopo-processo / não-categórico) é distinto da lista
    // vazia — nunca conflá-los, sob pena de o snapshot do change-tracker
    // materializar um nulo como '[]' e violar a invariante da ADR-0111. Igualdade
    // por sequência ordinal; snapshot preserva o nulo.
    private static readonly ValueComparer<IReadOnlyList<string>> ValoresDominioComparer =
        new(
            (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b, StringComparer.Ordinal),
            v => v == null
                ? 0
                : v.Aggregate(0, (acc, item) => HashCode.Combine(acc, StringComparer.Ordinal.GetHashCode(item))),
            v => v == null ? null! : v.ToList());

    private static List<string> DeserializarValores(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}

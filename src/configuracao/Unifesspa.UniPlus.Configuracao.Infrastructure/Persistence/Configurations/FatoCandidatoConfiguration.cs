namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Configuração EF Core do catálogo de fatos do candidato (<see cref="FatoCandidato"/>) — a tabela
/// <c>fatos_do_candidato</c> (UNI-REQ-0077, ADR-0111).
/// </summary>
/// <remarks>
/// <para>
/// O fato é desativado, nunca apagado, e a entidade deriva de <c>EntityBase</c> puro (sem
/// soft-delete). Por isso o índice único <c>ux_fatos_do_candidato_codigo</c> é
/// <strong>total</strong>: o código é chave natural imutável de uma linha que nunca é removida.
/// </para>
/// <para>
/// Os enums são persistidos como código de contrato UPPER_SNAKE por value converter (falha ao ler
/// um código desconhecido), cada um com um CHECK que restringe o texto ao domínio fechado.
/// <c>ponto_resolucao</c>/<c>binding</c> (ADR-0116) são referência por valor, sem FK.
/// </para>
/// </remarks>
public sealed class FatoCandidatoConfiguration : IEntityTypeConfiguration<FatoCandidato>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int CodigoDeContratoMaxLength = 20;
    private const int PontoResolucaoMaxLength = 50;
    private const int BindingMaxLength = 200;

    public void Configure(EntityTypeBuilder<FatoCandidato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("fatos_do_candidato", ConfigurarChecks);
        builder.HasKey(f => f.Id);
        ConfigurarIdentificacao(builder);
        ConfigurarClassificacaoDoFato(builder);
        ConfigurarProtecaoDeDados(builder);
        ConfigurarAprovacaoDaProtecao(builder);
        ConfigurarControle(builder);
        ConfigurarReferenciasPorValor(builder);
        ConfigurarRegrasPadrao(builder);
        ConfigurarDependencias(builder);
        ConfigurarValoresDeDominio(builder);
        ConfigurarIndice(builder);
        builder.HasData(MaterializarSeed());
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<FatoCandidato> builder)
    {
        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever força o
        // EF a tratar a chave como fornecida pela aplicação (convenção do repo).
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(f => f.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(f => f.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();
    }

    private static void ConfigurarClassificacaoDoFato(EntityTypeBuilder<FatoCandidato> builder)
    {
        ConfigurarComponenteEDominio(builder);
        ConfigurarOrigemCardinalidadeEEscopo(builder);
        ConfigurarFonteEFormato(builder);
    }

    private static void ConfigurarComponenteEDominio(EntityTypeBuilder<FatoCandidato> builder)
    {
        // componente de exibição: anulável — sem declaração vale o padrão que a consulta resolve.
        builder.Property(f => f.ComponenteExibicao)
            .HasConversion(ComponenteExibicaoConverter)
            .HasMaxLength(CodigoDeContratoMaxLength);

        builder.Property(f => f.Dominio)
            .HasConversion(DominioConverter)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();
    }

    private static void ConfigurarOrigemCardinalidadeEEscopo(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.Origem)
            .HasConversion(OrigemConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
        builder.Property(f => f.Cardinalidade)
            .HasConversion(CardinalidadeConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
        builder.Property(f => f.Escopo)
            .HasConversion(EscopoConverter).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
    }

    private static void ConfigurarFonteEFormato(EntityTypeBuilder<FatoCandidato> builder)
    {
        // fonte_valores (ADR-0136): anulável — só o fato categórico tem fonte; o booleano e o
        // numérico ficam com NULL, que o EF encapsula sem chamar o converter.
        builder.Property(f => f.FonteValores)
            .HasConversion(FonteValoresConverter)
            .HasMaxLength(CodigoDeContratoMaxLength);

        // formato (ADR-0136): anulável — só o fato de domínio texto tem formato.
        builder.Property(f => f.Formato)
            .HasConversion(FormatoConverter)
            .HasMaxLength(CodigoDeContratoMaxLength);
    }

    // Proteção de dados (ADR-0136): classificação na escala da ADR-0081, finalidade e
    // hipótese legal do tratamento, obrigatórias em todo fato.
    private static void ConfigurarProtecaoDeDados(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.ClassificacaoProtecao)
            .HasConversion<ClassificacaoDeProtecaoValueConverter>()
            .HasMaxLength(ClassificacaoDeProtecaoValueConverter.MaxLength)
            .IsRequired();
        builder.Property(f => f.FinalidadeTratamento).HasMaxLength(ProtecaoDeDados.FinalidadeMaxLength).IsRequired();
        builder.Property(f => f.HipoteseLegal)
            .HasConversion(ProtecaoDeDadosConversores.HipoteseLegal)
            .HasMaxLength(ProtecaoDeDadosConversores.HipoteseLegalMaxLength)
            .IsRequired();
    }

    // Aprovação da proteção de dados (ADR-0153): table splitting em colunas protecao_*. Todo fato
    // nasce pendente, o semeado inclusive; a semente dá só o valor inicial da proteção.
    private static void ConfigurarAprovacaoDaProtecao(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.OwnsOne(f => f.Aprovacao, ConfigurarColunasDaAprovacao);
        builder.Navigation(f => f.Aprovacao).IsRequired();
    }

    private static void ConfigurarColunasDaAprovacao(OwnedNavigationBuilder<FatoCandidato, AprovacaoDaProtecao> aprovacao)
    {
        AprovacaoDaProtecaoOwnedConfiguration.Configure(aprovacao);
        aprovacao.HasData(FatoCandidatoSeed.Itens.Select(item => new { FatoCandidatoId = item.Id, Situacao = SituacaoDaAprovacao.Pendente, Ciclo = CicloDaAprovacao.Primeiro, ManifestacaoDesfavoravelVigente = false, ManifestacoesNoCiclo = 0 }));
    }

    private static void ConfigurarControle(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.SemeadoPeloSistema).IsRequired();
        builder.Property(f => f.Ativo).IsRequired();
        builder.Property(f => f.CreatedBy).HasMaxLength(255);
        builder.Property(f => f.UpdatedBy).HasMaxLength(255);

        // Concorrência otimista pela coluna de sistema xmin (convenção do provider Npgsql, sem
        // coluna própria): duas edições concorrentes do mesmo fato não se sobrescrevem.
        builder.Property<uint>("Version").IsRowVersion();
    }

    // ponto_resolucao/binding (ADR-0116): referência por valor, sem FK — mesmo
    // padrão de dominio/origem/cardinalidade (código como valor, não linha viva).
    private static void ConfigurarReferenciasPorValor(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.PontoResolucao).HasMaxLength(PontoResolucaoMaxLength).IsRequired();
        builder.Property(f => f.Binding).HasMaxLength(BindingMaxLength).IsRequired();
    }

    // Regras padrão do derivado por regra (ADR-0136): lidas e gravadas sempre inteiras, junto do
    // fato, sem consulta por condição — por isso num documento, e não em linhas.
    private static void ConfigurarRegrasPadrao(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.RegrasPadrao)
            .HasColumnType("jsonb")
            .HasConversion(RegrasPadraoJson.Converter, RegrasPadraoJson.Comparer)
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();
    }

    // Dependências do derivado do sistema (ADR-0136), declaradas pelo seed a partir do mecanismo
    // que calcula o fato.
    private static void ConfigurarDependencias(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.Property(f => f.Dependencias)
            .HasColumnName("dependencias")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();
    }

    // Coleção filha (ADR-0116): descrição por valor de um categórico estático.
    // Cascade porque o filho não tem sentido sem o pai (mesmo padrão de
    // AtendimentoEspecializado.Condicoes).
    private static void ConfigurarValoresDeDominio(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.HasMany(f => f.ValoresDominioDeclarados)
            .WithOne()
            .HasForeignKey(v => v.FatoCandidatoId)
            .HasConstraintName("fk_valores_de_dominio_do_fato_fato_do_candidato")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.ValoresDominioDeclarados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // UNIQUE total do código (chave natural imutável): o fato é desativado, nunca apagado,
    // então não há slot a liberar — a unicidade é absoluta.
    private static void ConfigurarIndice(EntityTypeBuilder<FatoCandidato> builder)
    {
        builder.HasIndex(f => f.Codigo)
            .IsUnique()
            .HasDatabaseName(RestricoesDoBanco.FatoDoCandidatoCodigoUnico);
    }

    private static void ConfigurarChecks(TableBuilder<FatoCandidato> table)
    {
        ConfigurarChecksDosDominiosFechados(table);
        ConfigurarChecksDeCoerenciaDoDominio(table);
        AprovacaoDaProtecaoOwnedConfiguration.ConfigurarChecks(table, "fatos_do_candidato");
    }

    // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
    private static void ConfigurarChecksDosDominiosFechados(TableBuilder<FatoCandidato> table)
    {
        AdicionarCheckDeCodigos(table, "dominio", DominiosFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "origem", OrigensFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "cardinalidade", CardinalidadesFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "escopo", EscoposFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "classificacao_protecao", ClassificacoesDeProtecao.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "hipotese_legal", HipotesesLegaisTratamento.TokensCanonicos);
        table.HasCheckConstraint(
            "ck_fatos_do_candidato_componente_exibicao",
            $"componente_exibicao IS NULL OR componente_exibicao IN ({CodigosSql(ComponentesDeExibicao.TokensCanonicos)})");
    }

    private static void ConfigurarChecksDeCoerenciaDoDominio(TableBuilder<FatoCandidato> table)
    {
        // Coerência fonte_valores × domínio (invariante da factory, replicada no banco): o
        // categórico declara uma das fontes; os demais domínios não têm fonte.
        table.HasCheckConstraint(
            "ck_fatos_do_candidato_fonte_valores_coerente",
            $"(dominio = 'CATEGORICO' AND fonte_valores IS NOT NULL AND fonte_valores IN ({CodigosSql(FontesValoresFato.TokensCanonicos)})) "
            + "OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");

        table.HasCheckConstraint(
            "ck_fatos_do_candidato_formato_coerente",
            $"(dominio = 'TEXTO' AND formato IS NOT NULL AND formato IN ({CodigosSql(FormatosTexto.TokensCanonicos)})) "
            + "OR (dominio <> 'TEXTO' AND formato IS NULL)");

        ConfigurarCheckDaClassificacaoMinimaDoDominio(table);
    }

    // Texto, data e endereço nunca são menos que dado pessoal (invariante da factory), inclusive
    // o nome social (ADR-0082, ADR-0153). Só o domínio texto também
    // aceita `IDENTIFICADOR` — número de documento é sempre texto, nunca data, endereço nem os
    // demais domínios (ADR-0136, emenda de #1857).
    private static void ConfigurarCheckDaClassificacaoMinimaDoDominio(TableBuilder<FatoCandidato> table)
    {
        table.HasCheckConstraint(
            "ck_fatos_do_candidato_classificacao_minima_do_dominio",
            "(dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') "
            + "OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) "
            + "OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
    }

    private static void AdicionarCheckDeCodigos(TableBuilder<FatoCandidato> table, string coluna, IReadOnlyList<string> codigos) =>
        table.HasCheckConstraint(
            $"ck_fatos_do_candidato_{coluna}",
            $"{coluna} IN ({CodigosSql(codigos)})");

    private static string CodigosSql(IReadOnlyList<string> codigos) =>
        string.Join(", ", codigos.Select(codigo => $"'{codigo}'"));

    /// <summary>
    /// Projeta o seed (<see cref="FatoCandidatoSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration inicial. O instante é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>); mudar o seed muda o
    /// modelo, e a migration inicial é gerada de novo.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante fixo do seed (HasData exige valor determinístico).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return FatoCandidatoSeed.Itens.Select(item => new
        {
            item.Id,
            item.Codigo,
            item.Nome,
            item.Descricao,
            item.ComponenteExibicao,
            item.Dominio,
            item.Origem,
            item.Cardinalidade,
            item.FonteValores,
            item.Formato,
            item.PontoResolucao,
            item.Binding,
            item.Escopo,
            item.ClassificacaoProtecao,
            item.FinalidadeTratamento,
            FatoCandidatoSeed.HipoteseLegal,
            SemeadoPeloSistema = true,
            Ativo = true,
            RegrasPadrao = item.RegrasPadrao ?? (IReadOnlyList<RegraDerivacao>)[],
            Dependencias = DerivadosDoSistema.Dependencias.GetValueOrDefault(item.Codigo) ?? [],
            CreatedAt = seedCriadoEm,
        });
    }

    // ── Conversores/comparadores ──────────────────────────────────────────────

    private static readonly ValueConverter<DominioFato, string> DominioConverter =
        new(dominio => DominiosFato.ParaTokenCanonico(dominio), token => DominiosFato.Analisar(token));

    private static readonly ValueConverter<OrigemFato, string> OrigemConverter =
        new(origem => OrigensFato.ParaTokenCanonico(origem), token => OrigensFato.Analisar(token));

    private static readonly ValueConverter<FonteValoresFato, string> FonteValoresConverter =
        new(fonte => FontesValoresFato.ParaTokenCanonico(fonte), token => FontesValoresFato.Analisar(token));

    private static readonly ValueConverter<CardinalidadeFato, string> CardinalidadeConverter =
        new(cardinalidade => CardinalidadesFato.ParaTokenCanonico(cardinalidade), token => CardinalidadesFato.Analisar(token));

    private static readonly ValueConverter<ComponenteDeExibicao, string> ComponenteExibicaoConverter =
        new(componente => ComponentesDeExibicao.ParaTokenCanonico(componente), token => ComponentesDeExibicao.Analisar(token));

    private static readonly ValueConverter<FormatoTexto, string> FormatoConverter =
        new(formato => FormatosTexto.ParaTokenCanonico(formato), token => FormatosTexto.Analisar(token));

    private static readonly ValueConverter<EscopoFato, string> EscopoConverter =
        new(escopo => EscoposFato.ParaTokenCanonico(escopo), token => EscoposFato.Analisar(token));
}

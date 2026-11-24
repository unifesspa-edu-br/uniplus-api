namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    internal const string NomeParaBusca = "NomeParaBusca";
    internal const string SiglaParaBusca = "SiglaParaBusca";
    internal const string CodigoParaBusca = "CodigoParaBusca";
    internal const string IdentificadorNoEnderecoWebParaBusca = "IdentificadorNoEnderecoWebParaBusca";
    internal const string ApelidoParaBusca = "ApelidoParaBusca";

    private const int TipoMaxLength = 30;

    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        // Trio de cidade all-or-nothing (issue #1114): espelha no banco a invariante
        // de domínio já provada por Unidade.ValidarReferenciaCidade — mesmo padrão de
        // InstituicaoConfiguration.
        builder.ToTable("unidades", t =>
        {
            t.HasCheckConstraint(
                "ck_unidades_cidade_completa",
                "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) "
                + "OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
            t.HasCheckConstraint("ck_unidades_tipo", $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoUnidade>()})");
        });
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).HasMaxLength(250).IsRequired();
        builder.Property(u => u.Alias)
            .HasColumnName("apelido")
            .HasMaxLength(100)
            .HasComment("Nome pelo qual a unidade também é conhecida; opcional e pode se repetir entre unidades.");
        builder.Property(u => u.Slug)
            .HasColumnName("identificador_no_endereco_web")
            .HasConversion(new SlugValueConverter())
            .HasMaxLength(64)
            .HasComment("Texto que identifica a unidade no endereço de página da web; único entre as unidades ativas.")
            .IsRequired();
        builder.Property(u => u.Sigla).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Tipo)
            .HasMaxLength(TipoMaxLength)
            .HasComment("Tipo da unidade na estrutura da universidade. Códigos: REITORIA, PRO_REITORIA, CENTRO, INSTITUTO, FACULDADE, DEPARTAMENTO, COORDENACAO, DIRETORIA, DIVISAO, NUCLEO, OUTRO.")
            .IsRequired();

        builder.Property(u => u.UnidadeAcademica).IsRequired();
        builder.Property(u => u.VigenciaInicio).IsRequired();
        builder.Property(u => u.VigenciaFim);

        // Referência de cidade do Geo (ADR-0090, issue #1114) — código + display
        // cache, opcional all-or-nothing, sem FK cross-banco para uniplus_geo.
        builder.Property(u => u.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .HasComment("Código IBGE (7 dígitos) da cidade da Unidade — referência ao Geo, sem FK cross-banco; opcional all-or-nothing com nome/UF.");
        builder.Property(u => u.CidadeNome)
            .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
            .HasComment("Nome de exibição da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");
        builder.Property(u => u.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .HasComment("UF da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");

        // Auditoria (IAuditableEntity)
        builder.Property(u => u.CreatedBy).HasMaxLength(255);
        builder.Property(u => u.UpdatedBy).HasMaxLength(255);

        // Hierarquia: auto-referência intra-banco (ADR-0054)
        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(u => u.UnidadeSuperiorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Histórico de identificadores (append-only): NÃO cascatear (issue #629).
        // O histórico não implementa ISoftDeletable; com Cascade, remover a Unidade
        // marcaria os históricos carregados (ObterPorIdAsync faz Include) como Deleted
        // e — como o SoftDeleteInterceptor só converte ISoftDeletable — eles sofreriam
        // hard-delete físico, destruindo a trilha de auditoria.
        //
        // ClientNoAction (e não Restrict/NoAction): com o histórico required já
        // rastreado, Restrict/NoAction lançam "required relationship severed" ao
        // marcar a Unidade como Deleted — ANTES de o interceptor convertê-la em
        // soft-delete. ClientNoAction instrui o EF a não tocar nem validar os
        // dependentes; o interceptor então converte a Unidade em UPDATE (soft-delete)
        // e o histórico permanece intacto. A Unidade nunca é hard-deletada, logo a
        // integridade referencial é preservada na prática (FK NO ACTION no banco).
        builder.HasMany(u => u.Historico)
            .WithOne()
            .HasForeignKey(h => h.UnidadeId)
            .OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(u => u.Historico)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Índices únicos parciais (WHERE is_deleted = false) — unicidade entre vivos
        builder.HasIndex(u => u.Slug)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.UnidadeSlugVivo);

        builder.HasIndex(u => u.Sigla)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.UnidadeSiglaAtivos);

        builder.HasIndex(u => u.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.UnidadeCodigoAtivos);

        // Apelido: índice não-único (para agrupamento/busca)
        builder.HasIndex(u => u.Alias)
            .HasDatabaseName("ix_unidades_apelido");

        // Hierarquia: índice para busca de subordinadas
        builder.HasIndex(u => u.UnidadeSuperiorId)
            .HasDatabaseName("ix_unidades_superior_id");

        ConfigurarBuscaTextual(builder);
    }

    /// <summary>
    /// As formas normalizadas (sem acento e em minúsculas, pela função da plataforma) dos campos que a busca
    /// textual compara, geradas pelo banco, cada uma com o seu índice trigrama.
    /// </summary>
    private static void ConfigurarBuscaTextual(EntityTypeBuilder<Unidade> builder)
    {
        ColunaParaBusca(builder, NomeParaBusca, "nome", 250).IsRequired();
        ColunaParaBusca(builder, SiglaParaBusca, "sigla", 50).IsRequired();
        ColunaParaBusca(builder, CodigoParaBusca, "codigo", 50).IsRequired();
        ColunaParaBusca(builder, IdentificadorNoEnderecoWebParaBusca, "identificador_no_endereco_web", 64).IsRequired();

        // Sem apelido a coluna é nula: a função da plataforma devolve nulo para entrada nula.
        ColunaParaBusca(builder, ApelidoParaBusca, "apelido", 100);
    }

    private static PropertyBuilder<string?> ColunaParaBusca(
        EntityTypeBuilder<Unidade> builder, string propriedade, string colunaDeOrigem, int tamanho)
    {
        PropertyBuilder<string?> coluna = builder.Property<string?>(propriedade)
            .HasMaxLength(tamanho)
            .UseCollation("C")
            .HasComputedColumnSql(NormalizacaoTextual.ExpressaoSql(colunaDeOrigem), stored: true)
            .HasComment($"Forma normalizada de {colunaDeOrigem} (sem acento e em minúsculas, pela função da plataforma), "
                + "gerada pelo banco; a busca textual da Unidade compara contra ela.");

        builder.HasIndex(propriedade)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName($"ix_unidades_{colunaDeOrigem}_para_busca_trgm");

        return coluna;
    }
}

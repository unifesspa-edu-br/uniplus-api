namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core da trilha da contenção do perfil, registro só acrescentado. Os checks dão um ramo a cada tipo de
/// evento e, na retomada, um a cada origem, de modo que o banco recusa a referência de um ramo no registro de outro.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EventoDaContencaoConfiguration : IEntityTypeConfiguration<EventoDaContencao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;
    private const int PermissaoMaxLength = 120;

    private const string SemOrigem = "origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL";

    private const string DeUmaPessoa =
        $"ator = '{AtorDoEventoDaContencaoCodigo.Humano}' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL";

    private static readonly ValueConverter<TipoDoEventoDaContencao, string> TipoConverter = new(
        tipo => tipo.ToCodigo(),
        codigo => TipoDoEventoDaContencaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<AtorDoEventoDaContencao, string> AtorConverter = new(
        ator => ator.ToCodigo(),
        codigo => AtorDoEventoDaContencaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<OrigemDaRetomada, string> OrigemConverter = new(
        origem => origem.ToCodigo(),
        codigo => OrigemDaRetomadaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<EtapaDaVersao, string> PapelConverter = new(
        papel => papel.ToCodigo(),
        codigo => EtapaDaVersaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<EventoDaContencao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("eventos_da_contencao", ConfigurarRestricoes);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Instante).IsRequired();
        builder.Property(e => e.Tipo).HasConversion(TipoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(e => e.Ator).HasConversion(AtorConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(e => e.Origem).HasConversion(OrigemConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(e => e.PermissaoCodigo).HasMaxLength(PermissaoMaxLength);
        builder.Property(e => e.AtorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(e => e.AtorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(e => e.AtorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(e => e.Papel).HasConversion(PapelConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(e => e.Motivo).HasMaxLength(MotivosDaVersao.MaxLength);
        builder.Property(e => e.PerfilProtegido).IsRequired();
        builder.Property(e => e.OrdemNaTrilha).IsRequired();

        builder.Ignore(e => e.OrdinalLocal);

        ConfigurarReferencias(builder);

        builder.HasIndex(e => new { e.PerfilId, e.Instante }).HasDatabaseName("ix_eventos_da_contencao_perfil");
        builder.HasIndex(e => e.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_eventos_da_contencao_ordem");
    }

    private static void ConfigurarReferencias(EntityTypeBuilder<EventoDaContencao> builder)
    {
        builder.HasOne<SuspensaoDoPerfil>().WithMany().HasForeignKey(e => e.SuspensaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DesativacaoDoPerfil>().WithMany().HasForeignKey(e => e.DesativacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DecisaoDaRetomada>().WithMany().HasForeignKey(e => e.DecisaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>().WithMany().HasForeignKey(e => e.VersaoQueEntrouId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>().WithMany().HasForeignKey(e => e.VersaoVigenteId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarRestricoes(TableBuilder<EventoDaContencao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_eventos_da_contencao_tipo",
            $"tipo IN ('{TipoDoEventoDaContencaoCodigo.Suspensao}', '{TipoDoEventoDaContencaoCodigo.Retomada}',"
            + $" '{TipoDoEventoDaContencaoCodigo.Desativacao}', '{TipoDoEventoDaContencaoCodigo.Reativacao}')");
        tabela.HasCheckConstraint("ck_eventos_da_contencao_suspensao", RamoDaSuspensao());
        tabela.HasCheckConstraint("ck_eventos_da_contencao_retomada", RamoDaRetomada());
        tabela.HasCheckConstraint("ck_eventos_da_contencao_desativacao", RamoDaDesativacaoOuReativacao(TipoDoEventoDaContencaoCodigo.Desativacao));
        tabela.HasCheckConstraint("ck_eventos_da_contencao_reativacao", RamoDaDesativacaoOuReativacao(TipoDoEventoDaContencaoCodigo.Reativacao));
    }

    private static string RamoDaSuspensao() =>
        $"tipo <> '{TipoDoEventoDaContencaoCodigo.Suspensao}' OR ({DeUmaPessoa} AND suspensao_id IS NOT NULL AND desativacao_id IS NULL"
        + $" AND papel IS NOT NULL AND {SemOrigem})";

    private static string RamoDaDesativacaoOuReativacao(string tipo) =>
        $"tipo <> '{tipo}' OR ({DeUmaPessoa} AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND {SemOrigem})";

    private static string RamoDaRetomada() =>
        $"tipo <> '{TipoDoEventoDaContencaoCodigo.Retomada}' OR (ator = '{AtorDoEventoDaContencaoCodigo.Sistema}'"
        + " AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_sub IS NULL"
        + " AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL"
        + $" AND (({OrigemDe(OrigemDaRetomadaCodigo.DecisaoDaEtapa, "decisao_id", "versao_que_entrou_id", "permissao_codigo")})"
        + $" OR ({OrigemDe(OrigemDaRetomadaCodigo.TrocaDeVigencia, "versao_que_entrou_id", "decisao_id", "permissao_codigo")})"
        + $" OR ({OrigemDe(OrigemDaRetomadaCodigo.Reclassificacao, "permissao_codigo", "decisao_id", "versao_que_entrou_id")})))";

    // A origem preenche a própria referência e deixa nulas as dos outros dois ramos.
    private static string OrigemDe(string origem, string propria, string outra, string terceira) =>
        $"origem = '{origem}' AND {propria} IS NOT NULL AND {outra} IS NULL AND {terceira} IS NULL";
}

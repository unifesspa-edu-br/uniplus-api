namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
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

    private static readonly string DeUmaPessoa =
        $"ator = {SqlDosCodigosDeContrato.Literal(AtorDoEventoDaContencao.Humano)} AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL";

    public void Configure(EntityTypeBuilder<EventoDaContencao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("eventos_de_bloqueio_do_perfil", ConfigurarRestricoes);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Instante).IsRequired();
        builder.Property(e => e.Tipo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(e => e.Ator).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(e => e.Origem).HasMaxLength(CodigoMaxLength);
        builder.Property(e => e.PermissaoCodigo).HasMaxLength(PermissaoMaxLength);
        builder.Property(e => e.AtorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(e => e.AtorContaId).HasMaxLength(IdentidadeMaxLength);
        builder.Property(e => e.AtorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(e => e.Papel).HasMaxLength(CodigoMaxLength);
        builder.Property(e => e.Motivo).HasMaxLength(MotivoInformado.MaximoDeCaracteres);
        builder.Property(e => e.PerfilProtegido).IsRequired();
        builder.Property(e => e.OrdemNoHistorico).IsRequired();

        builder.Ignore(e => e.OrdinalLocal);

        ConfigurarReferencias(builder);

        builder.HasIndex(e => new { e.PerfilId, e.Instante }).HasDatabaseName("ix_eventos_de_bloqueio_do_perfil_perfil");
        builder.HasIndex(e => e.OrdemNoHistorico).IsUnique().HasDatabaseName("ux_eventos_de_bloqueio_do_perfil_ordem");
    }

    private static void ConfigurarReferencias(EntityTypeBuilder<EventoDaContencao> builder)
    {
        builder.HasOne<SuspensaoDoPerfil>().WithMany().HasForeignKey(e => e.SuspensaoId).HasConstraintName("fk_eventos_de_bloqueio_do_perfil_suspensao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DesativacaoDoPerfil>().WithMany().HasForeignKey(e => e.DesativacaoId).HasConstraintName("fk_eventos_de_bloqueio_do_perfil_desativacao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DecisaoDaRetomada>().WithMany().HasForeignKey(e => e.DecisaoId).HasConstraintName("fk_eventos_de_bloqueio_do_perfil_decisao_da_retomada").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>().WithMany().HasForeignKey(e => e.VersaoQueEntrouId).HasConstraintName("fk_eventos_de_bloqueio_do_perfil_versao_que_entrou").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>().WithMany().HasForeignKey(e => e.VersaoVigenteId).HasConstraintName("fk_eventos_de_bloqueio_do_perfil_versao_vigente").OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarRestricoes(TableBuilder<EventoDaContencao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_eventos_de_bloqueio_do_perfil_tipo",
            $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoDoEventoDaContencao>()})");
        tabela.HasCheckConstraint("ck_eventos_de_bloqueio_do_perfil_suspensao", RamoDaSuspensao());
        tabela.HasCheckConstraint("ck_eventos_de_bloqueio_do_perfil_retomada", RamoDaRetomada());
        tabela.HasCheckConstraint("ck_eventos_de_bloqueio_do_perfil_desativacao", RamoDaDesativacaoOuReativacao(SqlDosCodigosDeContrato.Literal(TipoDoEventoDaContencao.Desativacao)));
        tabela.HasCheckConstraint("ck_eventos_de_bloqueio_do_perfil_reativacao", RamoDaDesativacaoOuReativacao(SqlDosCodigosDeContrato.Literal(TipoDoEventoDaContencao.Reativacao)));
    }

    private static string RamoDaSuspensao() =>
        $"tipo <> {SqlDosCodigosDeContrato.Literal(TipoDoEventoDaContencao.Suspensao)} OR ({DeUmaPessoa} AND suspensao_id IS NOT NULL AND desativacao_id IS NULL"
        + $" AND papel IS NOT NULL AND {SemOrigem})";

    private static string RamoDaDesativacaoOuReativacao(string tipo) =>
        $"tipo <> {tipo} OR ({DeUmaPessoa} AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND {SemOrigem})";

    private static string RamoDaRetomada() =>
        $"tipo <> {SqlDosCodigosDeContrato.Literal(TipoDoEventoDaContencao.Retomada)} OR (ator = {SqlDosCodigosDeContrato.Literal(AtorDoEventoDaContencao.Sistema)}"
        + " AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_conta_id IS NULL"
        + " AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL AND origem IS NOT NULL"
        + $" AND (({OrigemDe(SqlDosCodigosDeContrato.Literal(OrigemDaRetomada.DecisaoDaEtapa), "decisao_id", "versao_que_entrou_id", "permissao_codigo")})"
        + $" OR ({OrigemDe(SqlDosCodigosDeContrato.Literal(OrigemDaRetomada.TrocaDeVigencia), "versao_que_entrou_id", "decisao_id", "permissao_codigo")})"
        + $" OR ({OrigemDe(SqlDosCodigosDeContrato.Literal(OrigemDaRetomada.Reclassificacao), "permissao_codigo", "decisao_id", "versao_que_entrou_id")})))";

    // A origem preenche a própria referência e deixa nulas as dos outros dois ramos.
    private static string OrigemDe(string origem, string propria, string outra, string terceira) =>
        $"origem = {origem} AND {propria} IS NOT NULL AND {outra} IS NULL AND {terceira} IS NULL";
}

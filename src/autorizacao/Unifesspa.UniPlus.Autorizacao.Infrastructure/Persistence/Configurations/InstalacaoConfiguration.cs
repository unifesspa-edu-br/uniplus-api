namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Configuração EF Core da instalação dos primeiros gestores de acessos.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class InstalacaoConfiguration : IEntityTypeConfiguration<Instalacao>
{
    private const int IdentidadeMaxLength = 255;
    private const int SituacaoMaxLength = 40;
    private const int ETagMaxLength = 200;
    private const int Sha256HexLength = 64;

    private static readonly ValueConverter<SituacaoDaInstalacao, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDaInstalacaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<Instalacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("instalacoes", tabela =>
        {
            tabela.HasCheckConstraint(
                "ck_instalacoes_tentativas",
                $"tentativas >= 0 AND tentativas <= {Instalacao.MaximoDeTentativas}");
            tabela.HasCheckConstraint(
                "ck_instalacoes_codigo_completo",
                "(codigo_enviado_em IS NULL) = (codigo_expira_em IS NULL)");
        });
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ExecutorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(i => i.ExecutorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(i => i.NumeroDoDocumento).HasMaxLength(Instalacao.NumeroDoDocumentoMaxLength).IsRequired();
        builder.Property(i => i.TamanhoDoPdf).IsRequired();
        builder.Property(i => i.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(i => i.DocumentoETag).HasColumnName("documento_etag").HasMaxLength(ETagMaxLength);
        builder.Property(i => i.DocumentoSha256).HasMaxLength(Sha256HexLength);
        builder.Property(i => i.CodigoResumo);
        builder.Property(i => i.Tentativas).IsRequired();

        // Quem executa e as datas são deriváveis do estado; a identidade do executor também se
        // calcula, não se grava de novo.
        builder.Ignore(i => i.Executor);
        builder.Ignore(i => i.Pendente);
        builder.Ignore(i => i.ChaveDeEnvio);
        builder.Ignore(i => i.ChaveDefinitiva);

        builder.Navigation(i => i.Gestores).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(i => i.Gestores, gestor =>
        {
            gestor.ToTable("instalacao_gestores");
            gestor.WithOwner().HasForeignKey("InstalacaoId");
            gestor.Property<Guid>("InstalacaoId");
            gestor.Property(g => g.Emissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
            gestor.Property(g => g.Sub).HasMaxLength(IdentidadeMaxLength).IsRequired();
            gestor.HasKey("InstalacaoId", nameof(GestorDaInstalacao.Emissor), nameof(GestorDaInstalacao.Sub));
        });

        // Uma instalação pendente por executor: iniciar de novo cancela a anterior antes de gravar
        // a nova, e este índice barra a corrida entre duas iniciações simultâneas.
        builder.HasIndex(i => new { i.ExecutorEmissor, i.ExecutorSub })
            .IsUnique()
            .HasFilter(
                "situacao IN ("
                + string.Join(", ", Instalacao.SituacoesPendentes.Select(s => $"'{s.ToCodigo()}'"))
                + ")")
            .HasDatabaseName("ux_instalacoes_executor_pendente");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }
}

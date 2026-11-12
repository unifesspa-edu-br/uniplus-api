namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core do perfil de acesso, das permissões próprias dos perfis de governança e do vínculo com as
/// versões e vigências, com a carga dos dois perfis de governança.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    /// <summary>Propriedade de sombra usada só para a unicidade do nome sem distinção de caixa.</summary>
    public const string NomeNormalizado = "NomeNormalizado";

    private const int NomeMaxLength = 120;
    private const int DescricaoMaxLength = 500;
    private const int GovernancaMaxLength = 40;
    private const int CodigoPermissaoMaxLength = 120;

    private static readonly ValueConverter<TipoDeGovernanca, string> GovernancaConverter = new(
        governanca => governanca.ToCodigo(),
        codigo => TipoDeGovernancaCodigo.FromCodigo(codigo));

    // Perfis de sistema, com identificador fixo para que as migrations seguintes os alcancem.
    private static readonly Guid GestorDeAcessosId = PerfisDeSistema.GestorDeAcessosId;
    private static readonly Guid RepresentanteDoControladorId = PerfisDeSistema.RepresentanteDoControladorId;
    private static readonly DateTimeOffset CargaInicial = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    // A contenção é evidência: o perfil não se exclui, e nenhum registro dela some em cascata.
    private static void ConfigurarContencao(EntityTypeBuilder<Perfil> builder)
    {
        builder.HasMany(p => p.Suspensoes).WithOne().HasForeignKey(s => s.PerfilId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Suspensoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Desativacoes).WithOne().HasForeignKey(d => d.PerfilId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Desativacoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Eventos).WithOne().HasForeignKey(e => e.PerfilId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Eventos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("perfis");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();
        builder.Property(p => p.Ativo).IsRequired();

        builder.Property(p => p.Governanca)
            .HasConversion(GovernancaConverter)
            .HasMaxLength(GovernancaMaxLength);

        // O nome é único sem distinguir maiúsculas de minúsculas. A coluna gerada guarda a
        // forma minúscula e o índice único recai sobre ela; o handler antecipa o conflito com
        // 409 e este índice barra a corrida.
        // Coluna sempre preenchida pelo banco; fica opcional no modelo para que a carga por
        // HasData não exija um valor que o banco gera.
        builder.Property<string?>(NomeNormalizado)
            .HasMaxLength(NomeMaxLength)
            .HasComputedColumnSql("lower(nome)", stored: true);

        builder.HasIndex(NomeNormalizado)
            .IsUnique()
            .HasDatabaseName("ux_perfis_nome");

        // Cada papel de governança pertence a um único perfil.
        builder.HasIndex(p => p.Governanca)
            .IsUnique()
            .HasDatabaseName("ux_perfis_governanca");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): duas edições a partir
        // da mesma versão não se misturam.
        builder.Property<uint>("Version").IsRowVersion();

        // Só os perfis de governança têm permissões próprias; o perfil comum as tem na versão.
        builder.Navigation(p => p.Permissoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A versão é evidência da composição: o perfil não se exclui e nenhuma versão some em cascata.
        builder.HasMany(p => p.Versoes).WithOne().HasForeignKey(v => v.PerfilId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Versoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Vigencias).WithOne().HasForeignKey(v => v.PerfilId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Vigencias).UsePropertyAccessMode(PropertyAccessMode.Field);

        ConfigurarContencao(builder);

        builder.OwnsMany(p => p.Permissoes, permissao =>
        {
            permissao.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            permissao.ToTable("perfil_permissoes");
            permissao.WithOwner().HasForeignKey("PerfilId");
            permissao.Property<Guid>("PerfilId");
            permissao.Property(x => x.Codigo).HasMaxLength(CodigoPermissaoMaxLength).IsRequired();
            permissao.HasKey("PerfilId", nameof(PermissaoDoPerfil.Codigo));

            permissao.HasData(ComposicaoDaGovernanca.Esperada.Select(static p => new { p.PerfilId, p.Codigo }));
        });

        builder.HasData(
            new
            {
                Id = GestorDeAcessosId,
                Nome = "Gestor de acessos",
                Descricao = "Opera as concessões de acesso, mantém os perfis de acesso e publica o termo de responsabilidade.",
                Ativo = true,
                Governanca = (TipoDeGovernanca?)TipoDeGovernanca.GestorDeAcessos,
                CreatedAt = CargaInicial,
            },
            new
            {
                Id = RepresentanteDoControladorId,
                Nome = "Representante do controlador",
                Descricao = "Decide, em nome da Unifesspa, a segunda etapa dos pedidos com dado pessoal, as versões protegidas de perfil, a janela de migração e a proteção do catálogo, e conduz a revisão periódica dos acessos.",
                Ativo = true,
                Governanca = (TipoDeGovernanca?)TipoDeGovernanca.RepresentanteDoControlador,
                CreatedAt = CargaInicial,
            });
    }
}

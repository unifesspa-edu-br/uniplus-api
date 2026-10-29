namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Configuração EF Core do perfil de acesso e de suas permissões, com a carga dos dois
/// perfis de governança.
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
    private static readonly Guid GestorDeAcessosId = new("019a0000-0000-7000-8000-000000000001");
    private static readonly Guid EncarregadaDeDadosId = new("019a0000-0000-7000-8000-000000000002");
    private static readonly DateTimeOffset CargaInicial = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

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

        builder.Navigation(p => p.Permissoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Permissoes, permissao =>
        {
            permissao.ToTable("perfil_permissoes");
            permissao.WithOwner().HasForeignKey("PerfilId");
            permissao.Property<Guid>("PerfilId");
            permissao.Property(x => x.Codigo).HasMaxLength(CodigoPermissaoMaxLength).IsRequired();
            permissao.HasKey("PerfilId", nameof(PermissaoDoPerfil.Codigo));

            permissao.HasData(
                new { PerfilId = GestorDeAcessosId, Codigo = "autorizacao:perfis:manter" },
                new { PerfilId = GestorDeAcessosId, Codigo = "autorizacao:permissoes:consultar-catalogo" },
                new { PerfilId = EncarregadaDeDadosId, Codigo = "autorizacao:permissoes:consultar-catalogo" });
        });

        builder.HasData(
            new
            {
                Id = GestorDeAcessosId,
                Nome = "Gestor de acessos",
                Descricao = "Opera as concessões de acesso e mantém os perfis de acesso.",
                Ativo = true,
                Governanca = (TipoDeGovernanca?)TipoDeGovernanca.GestorDeAcessos,
                CreatedAt = CargaInicial,
            },
            new
            {
                Id = EncarregadaDeDadosId,
                Nome = "Encarregada de dados",
                Descricao = "Aprova a concessão de acesso a dado pessoal ou sensível.",
                Ativo = true,
                Governanca = (TipoDeGovernanca?)TipoDeGovernanca.EncarregadaDeDados,
                CreatedAt = CargaInicial,
            });
    }
}

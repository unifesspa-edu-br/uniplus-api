namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Configurations;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

// Valida que AreaVisibilityConfiguration<TParent> registra a junction table
// no modelo EF com a forma de ADR-0060 (nome, PK composta, FK ON DELETE
// RESTRICT, índice parcial). O exclusion constraint GIST é SQL bruto e fica
// coberto pelo teste de integração.
public sealed class AreaVisibilityConfigurationTests
{
    private const string JunctionTable = "entidade_area_scoped_areas_de_interesse";
    private const string ParentFkColumn = "entidade_area_scoped_id";

    private readonly IEntityType _junction;

    public AreaVisibilityConfigurationTests()
    {
        using TestDbContext context = new();
        _junction = context.Model.FindEntityType(typeof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>))!;
    }

    [Fact]
    public void Configuracao_DeveRegistrarAJunctionTableNoModelo()
    {
        _junction.Should().NotBeNull("a config concreta herda a base e invoca ConfigureAreaVisibility");
        _junction.GetTableName().Should().Be(JunctionTable, "convenção: prefixo singular + _areas_de_interesse");
    }

    [Fact]
    public void Configuracao_DeveUsarPkCompostaParentIdAreaCodigoValidoDe()
    {
        IReadOnlyList<IProperty> pk = _junction.FindPrimaryKey()!.Properties;

        pk.Select(p => p.Name).Should().Equal(
            nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ParentId),
            nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.AreaCodigo),
            nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ValidoDe));
    }

    [Theory]
    [InlineData(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ParentId), ParentFkColumn)]
    [InlineData(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.AreaCodigo), "area_codigo")]
    [InlineData(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ValidoDe), "valid_from")]
    [InlineData(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ValidoAte), "valid_to")]
    [InlineData(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.AdicionadoPor), "added_by")]
    public void Configuracao_DeveMapearAsColunasComNomesDeAdr0060(string propriedade, string colunaEsperada)
    {
        StoreObjectIdentifier tabela = StoreObjectIdentifier.Table(JunctionTable);

        _junction.FindProperty(propriedade)!.GetColumnName(tabela)
            .Should().Be(colunaEsperada);
    }

    [Fact]
    public void Configuracao_DeveAplicarFkParaOPaiComOnDeleteRestrict()
    {
        IForeignKey fk = _junction.GetForeignKeys().Single();

        fk.PrincipalEntityType.ClrType.Should().Be<EntidadeAreaScopedFicticia>();
        fk.Properties.Single().Name.Should().Be(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ParentId));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict, "ADR-0060: a junction não cascateia com o pai");
    }

    [Fact]
    public void Configuracao_DeveCriarIndiceParcialDosVinculosVigentes()
    {
        IIndex indice = _junction.GetIndexes()
            .Single(i => i.GetFilter() is not null);

        indice.Properties.Select(p => p.Name).Should().Equal(
            nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.ParentId),
            nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.AreaCodigo));
        indice.GetFilter().Should().Be("valid_to IS NULL");
        indice.GetDatabaseName().Should().Be($"ix_{JunctionTable}_vigentes");
    }

    [Fact]
    public void Configuracao_DeveMapearAreaCodigoComoVarchar32ViaConvencao()
    {
        StoreObjectIdentifier tabela = StoreObjectIdentifier.Table(JunctionTable);

        // varchar(32) vem da convenção global ConfigureValueObjectConverters —
        // confirma que AreaCodigo na junction usa o mesmo mapeamento canônico.
        _junction.FindProperty(nameof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>.AreaCodigo))!
            .GetColumnType(tabela)
            .Should().Be("varchar(32)");
    }

    [Fact]
    public void DerivacaoDeNomes_DeveSeguirOPrefixoDaTabelaPai()
    {
        EntidadeAreaScopedFicticiaConfiguration config = new();

        config.JunctionTable.Should().Be(JunctionTable);
        config.ParentForeignKeyColumn.Should().Be(ParentFkColumn);
    }

    [Fact]
    public void Descoberta_ApplyConfigurationsFromAssembly_DeveAplicarAConfigParaOsDoisTipos()
    {
        // A config concreta implementa IEntityTypeConfiguration<TParent> E
        // <AreaDeInteresseBinding<TParent>>. ApplyConfigurationsFromAssembly
        // deve aplicá-la para os DOIS — é o mecanismo que F2 usa por entidade.
        using AssemblyScanTestDbContext context = new();

        context.Model.FindEntityType(typeof(EntidadeAreaScopedFicticia))
            .Should().NotBeNull("a config é descoberta e aplicada para a entidade pai");
        context.Model.FindEntityType(typeof(AreaDeInteresseBinding<EntidadeAreaScopedFicticia>))
            .Should().NotBeNull("e também para a junction, pela interface explícita da mesma config");
    }

    [Theory]
    [InlineData("PREFIXO_MAIUSCULO", "não é snake_case minúsculo")]
    [InlineData("1prefixo", "inicia por dígito")]
    [InlineData("prefixo-com-hifen", "hífen não é caractere de identificador")]
    [InlineData("prefixo com espaco", "espaço não é caractere de identificador")]
    [InlineData("_prefixo", "inicia por underscore, não por letra")]
    public void Construtor_DadoPrefixoComFormatoInvalido_DeveLancar(string prefixo, string razao)
    {
        Func<PrefixoProbe> criar = () => new PrefixoProbe(prefixo);

        criar.Should().Throw<ArgumentException>(razao);
    }

    [Fact]
    public void Construtor_DadoPrefixoLongoDemais_DeveLancar()
    {
        // 32 chars > limite de 31 — a constraint GIST derivada estouraria o
        // limite de 63 chars de identificador do PostgreSQL.
        Func<PrefixoProbe> criar = () => new PrefixoProbe(new string('a', 32));

        criar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construtor_DadoPrefixoSnakeCaseValido_NaoDeveLancar()
    {
        Func<PrefixoProbe> criar = () => new PrefixoProbe("tipo_documento");

        criar.Should().NotThrow();
    }

    // ─── Fixtures de teste ─────────────────────────────────────────────────

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada pelo EF Core.")]
    private sealed class EntidadeAreaScopedFicticia : EntityBase, IAreaScopedEntity
    {
        public AreaCodigo? Proprietario { get; private set; }

        public string Nome { get; private set; } = string.Empty;
    }

    private sealed class EntidadeAreaScopedFicticiaConfiguration
        : AreaVisibilityConfiguration<EntidadeAreaScopedFicticia>
    {
        // Construtor público explícito: ApplyConfigurationsFromAssembly
        // instancia a config via Activator.CreateInstance, que exige ctor público.
        public EntidadeAreaScopedFicticiaConfiguration()
            : base("entidade_area_scoped")
        {
        }

        public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<EntidadeAreaScopedFicticia> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("entidades_area_scoped_ficticias");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            ConfigureAreaVisibility(builder);
        }
    }

    // Probe que só exercita o construtor base de AreaVisibilityConfiguration —
    // a validação do prefixo dispara antes de qualquer Configure.
    private sealed class PrefixoProbe : AreaVisibilityConfiguration<EntidadeAreaScopedFicticia>
    {
        public PrefixoProbe(string prefixo)
            : base(prefixo)
        {
        }

        public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<EntidadeAreaScopedFicticia> builder)
            => ArgumentNullException.ThrowIfNull(builder);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada pelos testes para inspeção do modelo.")]
    private sealed class TestDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Connection string sintética — os testes só inspecionam o modelo,
            // nunca conectam.
            optionsBuilder.UseNpgsql("Host=stub;Database=stub;Username=stub;Password=stub");
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureValueObjectConverters();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            EntidadeAreaScopedFicticiaConfiguration config = new();
            modelBuilder.ApplyConfiguration<EntidadeAreaScopedFicticia>(config);
            modelBuilder.ApplyConfiguration<AreaDeInteresseBinding<EntidadeAreaScopedFicticia>>(config);
        }
    }

    // Variante que aplica a config via ApplyConfigurationsFromAssembly (em vez
    // de ApplyConfiguration manual), filtrando para SÓ a config de teste — evita
    // capturar configs de outros testes deste assembly.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada pelos testes para inspeção do modelo.")]
    private sealed class AssemblyScanTestDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=stub;Database=stub;Username=stub;Password=stub");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureValueObjectConverters();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(AreaVisibilityConfigurationTests).Assembly,
                type => type == typeof(EntidadeAreaScopedFicticiaConfiguration));
        }
    }
}

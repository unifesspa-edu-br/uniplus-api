namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

// Classificação de proteção declarada no modelo do EF e a conferência que a exige (ADR-0153).
public sealed class ClassificacaoDeProtecaoNoModeloTests
{
    private sealed class Pedido
    {
        public Guid Id { get; set; }

        public string Numero { get; set; } = string.Empty;

        public string? Motivo { get; set; }
    }

    private sealed class Dono
    {
        public Guid Id { get; set; }

        public List<Gestor> Gestores { get; set; } = [];
    }

    private sealed class Gestor
    {
        public string Sub { get; set; } = string.Empty;
    }

    private sealed class Composto
    {
        public Guid Id { get; set; }

        public Local Local { get; set; } = new();
    }

    private sealed class Local
    {
        public string Rua { get; set; } = string.Empty;
    }

    private sealed class Cadastro
    {
        public Guid Id { get; set; }

        public Complemento Complemento { get; set; } = new();
    }

    private sealed class Complemento
    {
        public string Nota { get; set; } = string.Empty;
    }

    private class Base
    {
        public Guid Id { get; set; }
    }

    private sealed class Derivada : Base
    {
        public string Extra { get; set; } = string.Empty;
    }

    private sealed class SemTabela
    {
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class Auditado : SoftDeletableEntity, IAuditableEntity
    {
        public string? CreatedBy { get; private set; }

        public string? UpdatedBy { get; private set; }
    }

    private sealed class ContextoSemDeclaracao(DbContextOptions<ContextoSemDeclaracao> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Pedido>().ToTable("pedidos");
    }

    private sealed class ContextoComPadraoEExcecao(DbContextOptions<ContextoComPadraoEExcecao> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Pedido> pedido = modelBuilder.Entity<Pedido>();
            pedido.ToTable("pedidos").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            pedido.Property(p => p.Motivo).ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        }
    }

    private sealed class ContextoRebaixado(DbContextOptions<ContextoRebaixado> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Pedido> pedido = modelBuilder.Entity<Pedido>();
            pedido.ToTable("pedidos").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            pedido.Property(p => p.Motivo).ClassificaComo(ClassificacaoDeProtecao.Interno);
        }
    }

    private sealed class ContextoComComplexProperty(DbContextOptions<ContextoComComplexProperty> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Composto> composto = modelBuilder.Entity<Composto>();
            composto.ToTable("compostos").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            composto.ComplexProperty(c => c.Local);
        }
    }

    private sealed class ContextoComTabelaDeFragmento(DbContextOptions<ContextoComTabelaDeFragmento> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Pedido> pedido = modelBuilder.Entity<Pedido>();
            pedido.ToTable("pedidos").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            pedido.SplitToTable("pedidos_extra", t => t.Property(p => p.Motivo));
        }
    }

    private sealed class ContextoComAuditoriaRebaixada(DbContextOptions<ContextoComAuditoriaRebaixada> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Auditado> auditado = modelBuilder.Entity<Auditado>();
            auditado.ToTable("auditados").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            auditado.Property(a => a.CreatedBy).ClassificaComo(ClassificacaoDeProtecao.Publico);
        }
    }

    private interface IVarianteCompartilhada
    {
        static abstract ClassificacaoDeProtecao? Dono { get; }

        static abstract ClassificacaoDeProtecao? Owned { get; }
    }

    private sealed class DonoPessoalOwnedInterno : IVarianteCompartilhada
    {
        public static ClassificacaoDeProtecao? Dono => ClassificacaoDeProtecao.Pessoal;

        public static ClassificacaoDeProtecao? Owned => ClassificacaoDeProtecao.Interno;
    }

    private sealed class DonoInternoOwnedPessoal : IVarianteCompartilhada
    {
        public static ClassificacaoDeProtecao? Dono => ClassificacaoDeProtecao.Interno;

        public static ClassificacaoDeProtecao? Owned => ClassificacaoDeProtecao.Pessoal;
    }

    private sealed class DonoSemDeclaracao : IVarianteCompartilhada
    {
        public static ClassificacaoDeProtecao? Dono => null;

        public static ClassificacaoDeProtecao? Owned => ClassificacaoDeProtecao.Interno;
    }

    private sealed class OwnedSemDeclaracao : IVarianteCompartilhada
    {
        public static ClassificacaoDeProtecao? Dono => ClassificacaoDeProtecao.Interno;

        public static ClassificacaoDeProtecao? Owned => null;
    }

    // Um tipo fechado por variante: o EF guarda um modelo por tipo de contexto.
    private sealed class ContextoCompartilhado<TVariante>(DbContextOptions<ContextoCompartilhado<TVariante>> options) : DbContext(options)
        where TVariante : IVarianteCompartilhada
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Cadastro> cadastro = modelBuilder.Entity<Cadastro>();
            cadastro.ToTable("cadastros");
            if (TVariante.Dono is { } dono)
            {
                cadastro.ClassificaPorPadrao(dono);
            }

            cadastro.OwnsOne(c => c.Complemento, o =>
            {
                if (TVariante.Owned is { } owned)
                {
                    o.ClassificaPorPadrao(owned);
                }
            });
        }
    }

    private sealed class ContextoComTabelaEView(DbContextOptions<ContextoComTabelaEView> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Pedido>().ToTable("pedidos").ToView("pedidos_v").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
    }

    private sealed class ContextoComHerancaTpt(DbContextOptions<ContextoComHerancaTpt> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Base>().UseTptMappingStrategy().ToTable("bases").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            modelBuilder.Entity<Derivada>().ToTable("derivadas");
        }
    }

    private sealed class ContextoComHerancaTpc(DbContextOptions<ContextoComHerancaTpc> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Base>().UseTpcMappingStrategy().ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            modelBuilder.Entity<Derivada>().ToTable("derivadas");
        }
    }

    private sealed class ContextoComTipoSemTabela(DbContextOptions<ContextoComTipoSemTabela> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<SemTabela> tipo = modelBuilder.Entity<SemTabela>();
            tipo.HasNoKey().ToTable((string?)null).ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        }
    }

    private sealed class ContextoComOwnedSemDeclaracao(DbContextOptions<ContextoComOwnedSemDeclaracao> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Dono> dono = modelBuilder.Entity<Dono>();
            dono.ToTable("donos").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            dono.OwnsMany(d => d.Gestores, g =>
            {
                g.ToTable("gestores");
                g.WithOwner().HasForeignKey("DonoId");
                g.Property<int>("Id");
                g.HasKey("Id");
            });
        }
    }

    private sealed class ContextoComAuditoria(DbContextOptions<ContextoComAuditoria> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Auditado>().ToTable("auditados").ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
    }

    private static Dictionary<string, ClassificacaoDeProtecao?> Colunas<TContext>(Func<DbContextOptions<TContext>, TContext> criar)
        where TContext : DbContext
    {
        using TContext contexto = criar(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<TContext>());
        return ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Retrato
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(linha => linha.Split(": "))
            .ToDictionary(p => p[0], p => Enum.TryParse(p[1], out ClassificacaoDeProtecao c) ? (ClassificacaoDeProtecao?)c : null);
    }

    [Fact(DisplayName = "Tipo mapeado sem declaração reprova a conferência e nomeia a tabela e a coluna")]
    public void SemDeclaracao_ReprovaNomeandoTabelaEColuna()
    {
        using ContextoSemDeclaracao contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoSemDeclaracao>());

        IReadOnlyList<string> violacoes = ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes;

        violacoes.Should().Contain(v => v.StartsWith("pedidos.motivo:", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Coluna segue o padrão do tipo e a anotada que difere dele vale a própria")]
    public void ColunaSegueOPadraoEAnotadaDifere()
    {
        Dictionary<string, ClassificacaoDeProtecao?> colunas = Colunas<ContextoComPadraoEExcecao>(o => new(o));

        colunas["pedidos.numero"].Should().Be(ClassificacaoDeProtecao.Interno);
        colunas["pedidos.motivo"].Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Fact(DisplayName = "Owned type não herda o padrão do dono e reprova sem declaração própria")]
    public void OwnedTypeNaoHerdaOPadraoDoDono()
    {
        Dictionary<string, ClassificacaoDeProtecao?> colunas = Colunas<ContextoComOwnedSemDeclaracao>(o => new(o));

        colunas["donos.id"].Should().Be(ClassificacaoDeProtecao.Interno);
        colunas["gestores.sub"].Should().BeNull();
    }

    [Fact(DisplayName = "Membros de auditoria resolvem Pessoal pela interface e pela base, sem anotação")]
    public void MembrosDeAuditoriaResolvemPessoal()
    {
        Dictionary<string, ClassificacaoDeProtecao?> colunas = Colunas<ContextoComAuditoria>(o => new(o));

        colunas["auditados.created_by"].Should().Be(ClassificacaoDeProtecao.Pessoal);
        colunas["auditados.updated_by"].Should().Be(ClassificacaoDeProtecao.Pessoal);
        colunas["auditados.deleted_by"].Should().Be(ClassificacaoDeProtecao.Pessoal);
        colunas["auditados.created_at"].Should().Be(ClassificacaoDeProtecao.Interno);
    }

    [Fact(DisplayName = "Anotação não rebaixa membro de auditoria abaixo de Pessoal")]
    public void AnotacaoNaoRebaixaAuditoria()
    {
        Dictionary<string, ClassificacaoDeProtecao?> colunas = Colunas<ContextoComAuditoriaRebaixada>(o => new(o));

        colunas["auditados.created_by"].Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Theory(DisplayName = "Coluna compartilhada entre dono e owned type resolve a mais alta, em qualquer ordem")]
    [InlineData(0)]
    [InlineData(1)]
    public void ColunaCompartilhadaResolveAMaisAlta(int caso)
    {
        Dictionary<string, ClassificacaoDeProtecao?> colunas = caso == 0
            ? Colunas<ContextoCompartilhado<DonoPessoalOwnedInterno>>(o => new(o))
            : Colunas<ContextoCompartilhado<DonoInternoOwnedPessoal>>(o => new(o));

        colunas["cadastros.id"].Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Theory(DisplayName = "Coluna compartilhada com uma das partes sem declaração reprova e nomeia o tipo, em qualquer ordem")]
    [InlineData(0)]
    [InlineData(1)]
    public void ColunaCompartilhadaComParteSemDeclaracaoReprova(int caso)
    {
        DbContext contexto = caso == 0
            ? new ContextoCompartilhado<DonoSemDeclaracao>(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoCompartilhado<DonoSemDeclaracao>>())
            : new ContextoCompartilhado<OwnedSemDeclaracao>(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoCompartilhado<OwnedSemDeclaracao>>());
        using (contexto)
        {
            string violacao = ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
                .Should().ContainSingle(v => v.StartsWith("cadastros.id:", StringComparison.Ordinal)).Subject;

            if (caso == 0)
            {
                violacao.Should().Contain(nameof(Cadastro)).And.NotContain(nameof(Complemento));
            }
            else
            {
                violacao.Should().Contain(nameof(Complemento));
            }
        }
    }

    [Fact(DisplayName = "Tipo mapeado a tabela e a view ao mesmo tempo reprova a conferência")]
    public void TabelaEViewReprova()
    {
        using ContextoComTabelaEView contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComTabelaEView>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("tabela e a view", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O retrato muda quando uma classificação é rebaixada")]
    public void RetratoRegistraRebaixamento()
    {
        using ContextoComPadraoEExcecao original = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComPadraoEExcecao>());
        using ContextoRebaixado rebaixado = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoRebaixado>());

        string retratoOriginal = ConferenciaDaClassificacaoDoModelo.Conferir(original.GetService<IDesignTimeModel>().Model).Retrato;
        string retratoRebaixado = ConferenciaDaClassificacaoDoModelo.Conferir(rebaixado.GetService<IDesignTimeModel>().Model).Retrato;

        retratoRebaixado.Should().NotBe(retratoOriginal);
        retratoOriginal.Should().Contain("pedidos.motivo: Pessoal\n");
        retratoRebaixado.Should().Contain("pedidos.motivo: Interno\n");
    }

    [Fact(DisplayName = "Complex property reprova a conferência em vez de ser ignorada")]
    public void ComplexPropertyReprova()
    {
        using ContextoComComplexProperty contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComComplexProperty>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("complex property", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Tabela de fragmento reprova a conferência em vez de ser ignorada")]
    public void TabelaDeFragmentoReprova()
    {
        using ContextoComTabelaDeFragmento contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComTabelaDeFragmento>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("SplitToTable", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Propriedade sem coluna na tabela do tipo reprova a conferência")]
    public void PropriedadeSemColunaReprova()
    {
        using ContextoComTabelaDeFragmento contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComTabelaDeFragmento>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("Pedido.Motivo: propriedade sem coluna", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Herança mapeada como TPT reprova a conferência em vez de omitir as colunas herdadas")]
    public void HerancaTptReprova()
    {
        using ContextoComHerancaTpt contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComHerancaTpt>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("TPT ou TPC", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Herança mapeada como TPC reprova a conferência em vez de omitir as colunas herdadas")]
    public void HerancaTpcReprova()
    {
        using ContextoComHerancaTpc contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComHerancaTpc>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("TPT ou TPC", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Tipo mapeado sem tabela nem view reprova a conferência")]
    public void TipoSemTabelaReprova()
    {
        using ContextoComTipoSemTabela contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ContextoComTipoSemTabela>());

        ConferenciaDaClassificacaoDoModelo.Conferir(contexto.GetService<IDesignTimeModel>().Model).Violacoes
            .Should().Contain(v => v.Contains("sem tabela nem view", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Declarar a classificação Nenhuma é recusado")]
    public void DeclararNenhumaERecusado()
    {
        ModelBuilder modelBuilder = new();

        Action declarar = () => modelBuilder.Entity<Pedido>().ClassificaPorPadrao(ClassificacaoDeProtecao.Nenhuma);

        declarar.Should().Throw<ArgumentOutOfRangeException>();
    }
}

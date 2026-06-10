namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

// Comportamento da convenção AplicarFiltroGlobalSoftDelete (issue #629):
// aplica `!IsDeleted` a tipos ISoftDeletable e ignora os demais. Cobre CA-05
// (consulta padrão exclui deletados; IgnoreQueryFilters expõe).
public sealed class ModelBuilderSoftDeleteExtensionsTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed class EntidadeSoft : SoftDeletableEntity
    {
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class EntidadeNaoSoft : EntityBase
    {
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class ContextoConvencao(DbContextOptions<ContextoConvencao> options) : DbContext(options)
    {
        public DbSet<EntidadeSoft> Softs => Set<EntidadeSoft>();
        public DbSet<EntidadeNaoSoft> NaoSofts => Set<EntidadeNaoSoft>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AplicarFiltroGlobalSoftDelete();
            base.OnModelCreating(modelBuilder);
        }
    }

    private static ContextoConvencao CriarContexto() =>
        new(new DbContextOptionsBuilder<ContextoConvencao>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact(DisplayName = "CA-05: consulta padrão exclui ISoftDeletable marcada como excluída; IgnoreQueryFilters expõe")]
    public void Convencao_FiltraExcluidos_IgnoreQueryFiltersExpoe()
    {
        using ContextoConvencao contexto = CriarContexto();
        EntidadeSoft viva = new() { Nome = "viva" };
        EntidadeSoft excluida = new() { Nome = "excluída" };
        excluida.MarkAsDeleted("auditor", Instante);
        contexto.Softs.AddRange(viva, excluida);
        contexto.SaveChanges();

        List<EntidadeSoft> visiveis = [.. contexto.Softs];
        visiveis.Should().ContainSingle("o filtro de convenção exclui registros is_deleted = true")
            .Which.Id.Should().Be(viva.Id);

        List<EntidadeSoft> todas = [.. contexto.Softs.IgnoreQueryFilters()];
        todas.Should().HaveCount(2, "IgnoreQueryFilters() remove o filtro e expõe a linha excluída");
    }

    [Fact(DisplayName = "Convenção não filtra nem mapeia coluna em entidade que não implementa ISoftDeletable")]
    public void Convencao_NaoFiltra_EntidadeNaoSoft()
    {
        using ContextoConvencao contexto = CriarContexto();
        contexto.NaoSofts.AddRange(
            new EntidadeNaoSoft { Nome = "a" },
            new EntidadeNaoSoft { Nome = "b" });
        contexto.SaveChanges();

        List<EntidadeNaoSoft> todas = [.. contexto.NaoSofts];
        todas.Should().HaveCount(2);

        contexto.Model.FindEntityType(typeof(EntidadeNaoSoft))!
            .FindProperty(nameof(ISoftDeletable.IsDeleted))
            .Should().BeNull("entidade sem ISoftDeletable não mapeia a coluna is_deleted");
    }
}

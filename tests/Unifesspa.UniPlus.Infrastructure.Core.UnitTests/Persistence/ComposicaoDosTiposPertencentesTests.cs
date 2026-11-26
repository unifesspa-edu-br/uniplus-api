namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class ComposicaoDosTiposPertencentesTests
{
    [Fact]
    public void Modelo_TipoPertencenteCompartilhado_PermaneceNaTabelaEEsquemaDeCadaDono()
    {
        using ContextoDeTeste contexto = ContextoDeTeste.Criar(new PrimeiroModulo(), new SegundoModulo());
        IModel modelo = contexto.GetService<IDesignTimeModel>().Model;

        ConferirPertencente<PrimeiroDono>(modelo, "primeiro", "primeiros");
        ConferirPertencente<SegundoDono>(modelo, "segundo", "segundos");
    }

    private static void ConferirPertencente<TDono>(IModel modelo, string esquema, string tabela)
    {
        IEntityType dono = modelo.FindEntityType(typeof(TDono))!;
        IEntityType pertencente = dono.FindNavigation("Detalhe")!.TargetEntityType;
        dono.GetSchema().Should().Be(esquema);
        dono.GetTableName().Should().Be(tabela);
        pertencente.GetSchema().Should().Be(esquema);
        pertencente.GetTableName().Should().Be(tabela);
    }

    private sealed class PrimeiroModulo : IContribuicaoDoModulo
    {
        public string Esquema => "primeiro";
        public void AplicarAoModelo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PrimeiroDono>().ToTable("primeiros").OwnsOne(dono => dono.Detalhe);
        }
    }

    private sealed class SegundoModulo : IContribuicaoDoModulo
    {
        public string Esquema => "segundo";
        public void AplicarAoModelo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SegundoDono>().ToTable("segundos").OwnsOne(dono => dono.Detalhe);
        }
    }

    private sealed class PrimeiroDono
    {
        public Guid Id { get; set; }
        public DetalheCompartilhado Detalhe { get; set; } = null!;
    }

    private sealed class SegundoDono
    {
        public Guid Id { get; set; }
        public DetalheCompartilhado Detalhe { get; set; } = null!;
    }

    private sealed class DetalheCompartilhado
    {
        public string Valor { get; set; } = string.Empty;
    }
}

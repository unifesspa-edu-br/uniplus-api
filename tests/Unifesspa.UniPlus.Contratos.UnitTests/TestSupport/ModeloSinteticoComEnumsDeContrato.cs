namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ModalidadeDeOferta
{
    [CodigoDeContrato("em_sala", "Em sala")]
    Presencial = 1,

    [CodigoDeContrato("a_distancia", "À distância")]
    Remota = 2,
}

public enum TurnoDeOferta
{
    Manha = 1,
    Tarde = 2,
}

internal sealed class Oferta
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public TurnoDeOferta Turno { get; set; }

    public ModalidadeDeOferta Modalidade { get; set; }

    public ModalidadeDeOferta? Alternativa { get; set; }

    public Local Local { get; set; } = new();

    public Material Material { get; set; } = new();

    public List<ModalidadeDeOferta> Preferidas { get; set; } = [];
}

internal sealed class Local
{
    public ModalidadeDeOferta Uso { get; set; }
}

internal sealed class Material
{
    public ModalidadeDeOferta Formato { get; set; }

    public Rodape Rodape { get; set; } = new();
}

internal sealed class Rodape
{
    public ModalidadeDeOferta Impressao { get; set; }
}

/// <summary>
/// Modelo que guarda o enum com código de contrato nos cinco lugares possíveis, sem nenhuma conversão configurada:
/// escalar, escalar opcional, tipo próprio (<c>owned</c>), tipo complexo aninhado dentro de tipo complexo e coleção primitiva.
/// </summary>
internal static class ModeloSinteticoComEnumsDeContrato
{
    internal static IModel ModeloSemAConvencao()
    {
        using ContextoSemAConvencao contexto = new();

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    internal static IModel ModeloComAConvencao()
    {
        using ContextoComAConvencao contexto = new();

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private class ContextoSemAConvencao() : DbContext(
        new DbContextOptionsBuilder().UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x").Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<Oferta>(oferta =>
            {
                oferta.OwnsOne(o => o.Local);
                oferta.ComplexProperty(o => o.Material, material => material.ComplexProperty(m => m.Rodape));
                oferta.PrimitiveCollection(o => o.Preferidas);
            });
        }
    }

    private sealed class ContextoComAConvencao : ContextoSemAConvencao
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
        }
    }
}

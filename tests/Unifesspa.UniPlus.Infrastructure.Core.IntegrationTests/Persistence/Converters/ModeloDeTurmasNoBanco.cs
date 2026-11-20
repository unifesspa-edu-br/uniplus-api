namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

// O código declarado difere do nome e do número do membro, para que o texto gravado diga qual dos três foi escrito.
public enum FormatoDeAula
{
    [CodigoDeContrato("em_sala", "Em sala")]
    Presencial = 1,

    [CodigoDeContrato("a_distancia", "À distância")]
    Remoto = 2,
}

internal sealed class Turma
{
    public Guid Id { get; set; }

    public FormatoDeAula Modalidade { get; set; }

    public FormatoDeAula? Alternativa { get; set; }

    public List<FormatoDeAula> Preferidas { get; set; } = [];
}

internal sealed class Oferta
{
    public Guid Id { get; set; }

    public MaterialDaOferta Material { get; set; } = new();
}

internal sealed class MaterialDaOferta
{
    public FormatoDeAula Formato { get; set; }

    public RodapeDoMaterial Rodape { get; set; } = new();
}

internal sealed class RodapeDoMaterial
{
    public FormatoDeAula Formato { get; set; }

    public FormatoDeAula? Alternativa { get; set; }

    public List<FormatoDeAula> Preferidas { get; set; } = [];
}

/// <summary>
/// Guarda o enum com código de contrato nos três lugares que viram coluna: escalar, escalar opcional e coleção
/// primitiva, na entidade e dentro de um tipo complexo aninhado. Nenhum deles configura conversão: quem instala o conversor do código é a convenção.
/// </summary>
internal sealed class ContextoDeTurmas(DbContextOptions<ContextoDeTurmas> opcoes) : DbContext(opcoes)
{
    internal const string Tabela = "turmas_com_enum_de_contrato";

    internal const string TabelaDeOfertas = "ofertas_com_complexo_de_contrato";

    public DbSet<Turma> Turmas => Set<Turma>();

    public DbSet<Oferta> Ofertas => Set<Oferta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Turma>(MapearTurma);
        modelBuilder.Entity<Oferta>(MapearOferta);

        modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
    }

    private static void MapearTurma(EntityTypeBuilder<Turma> turma)
    {
        turma.ToTable(Tabela);
        turma.Property(t => t.Id).HasColumnName("id");
        turma.Property(t => t.Modalidade).HasColumnName("modalidade");
        turma.Property(t => t.Alternativa).HasColumnName("alternativa");
        turma.PrimitiveCollection(t => t.Preferidas).HasColumnName("preferidas");
    }

    private static void MapearOferta(EntityTypeBuilder<Oferta> oferta)
    {
        oferta.ToTable(TabelaDeOfertas);
        oferta.Property(o => o.Id).HasColumnName("id");
        oferta.ComplexProperty(o => o.Material, material => material.ComplexProperty(m => m.Rodape));
    }
}

namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using Microsoft.EntityFrameworkCore;

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

/// <summary>
/// Guarda o enum com código de contrato nos três lugares que viram coluna: escalar, escalar opcional e coleção
/// primitiva. Nenhum deles configura conversão: quem instala o conversor do código é a convenção.
/// </summary>
internal sealed class ContextoDeTurmas(DbContextOptions<ContextoDeTurmas> opcoes) : DbContext(opcoes)
{
    internal const string Tabela = "turmas_com_enum_de_contrato";

    public DbSet<Turma> Turmas => Set<Turma>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Turma>(turma =>
        {
            turma.ToTable(Tabela);
            turma.Property(t => t.Id).HasColumnName("id");
            turma.Property(t => t.Modalidade).HasColumnName("modalidade");
            turma.Property(t => t.Alternativa).HasColumnName("alternativa");
            turma.PrimitiveCollection(t => t.Preferidas).HasColumnName("preferidas");
        });

        modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
    }
}

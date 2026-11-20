namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

internal sealed class Turma
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public int Vagas { get; set; }

    public Guid Responsavel { get; set; }

    public FormatoDeAula Modalidade { get; set; }

    public FormatoDeAula? Substituta { get; set; }

    public SituacaoDaTurma Situacao { get; set; }

    public TurnoDeAula Periodo { get; set; }
}

internal sealed class TurmaComSala
{
    public Guid Id { get; set; }

    public Sala Sala { get; set; } = new();
}

internal sealed class Sala
{
    public FormatoDeAula Uso { get; set; }
}

internal sealed class TurmaComMaterial
{
    public Guid Id { get; set; }

    public Material Material { get; set; } = new();
}

internal sealed class Material
{
    public FormatoDeAula Formato { get; set; }

    public Rodape Rodape { get; set; } = new();
}

internal sealed class Rodape
{
    public FormatoDeAula Impressao { get; set; }
}

internal sealed class TurmaComListas
{
    public Guid Id { get; set; }

    public List<FormatoDeAula> Preferidas { get; set; } = [];

    public List<FormatoDeAula?> Eventuais { get; set; } = [];

    public FormatoDeAula[] Fixas { get; set; } = [];
}

internal sealed class TurmaComAnotacoes
{
    public Guid Id { get; set; }

    public List<Anotacao> Anotacoes { get; set; } = [];
}

internal sealed class Anotacao
{
    public FormatoDeAula Formato { get; set; }

    public string Texto { get; set; } = string.Empty;
}

/// <summary>
/// Registra o enum com código em cada lugar do modelo onde ele pode aparecer, sem nenhuma conversão configurada: escalar,
/// escalar opcional, tipo próprio, tipo complexo aninhado e coleção primitiva, de lista e de vetor.
/// </summary>
internal sealed class ModuloComEnumsDeContrato : IContribuicaoDoModulo
{
    public string Esquema => "turmas";

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Turma>();
        modelBuilder.Entity<TurmaComSala>().OwnsOne(turma => turma.Sala);
        modelBuilder.Entity<TurmaComMaterial>().ComplexProperty(turma => turma.Material, material => material.ComplexProperty(m => m.Rodape));
        modelBuilder.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Preferidas);
        modelBuilder.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Eventuais);
        modelBuilder.Entity<TurmaComListas>().PrimitiveCollection(turma => turma.Fixas);
    }
}

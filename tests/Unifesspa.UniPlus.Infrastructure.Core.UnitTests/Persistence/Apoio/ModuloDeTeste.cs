namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

internal sealed class EntidadeDoModuloA
{
    public Guid Id { get; set; }
}

internal sealed class EntidadeDoModuloB
{
    public Guid Id { get; set; }
}

internal sealed class ModuloDeTeste<TEntidade>(string esquema) : IContribuicaoDoModulo
    where TEntidade : class
{
    public string Esquema { get; } = esquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TEntidade>().HasKey("Id");
    }
}

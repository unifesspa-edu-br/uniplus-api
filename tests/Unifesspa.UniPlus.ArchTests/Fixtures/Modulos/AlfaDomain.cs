namespace Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>Entidade do módulo sintético Alfa.</summary>
internal sealed class EntidadeDeAlfa
{
    public Guid Id { get; init; }

    public string Nome { get; set; } = string.Empty;
}

/// <summary>Mapeamento do EF que mora no domínio: fora do lugar, e a regra de localização tem de reprová-lo.</summary>
internal sealed class MapeamentoNoDominio : IEntityTypeConfiguration<EntidadeDeAlfa>
{
    public void Configure(EntityTypeBuilder<EntidadeDeAlfa> builder) => builder.HasKey(e => e.Id);
}

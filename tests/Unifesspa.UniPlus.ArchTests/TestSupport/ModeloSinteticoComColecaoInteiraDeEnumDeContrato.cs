namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

internal sealed class OfertaComColecaoInteira
{
    public Guid Id { get; set; }

    public List<ModalidadeDeOferta> Preferidas { get; set; } = [];

    public List<ModalidadeDeOferta?> Eventuais { get; set; } = [];
}

/// <summary>
/// Modelo em que o conversor está na coleção inteira de enum com código de contrato, e não no elemento de uma coleção
/// primitiva. É montado sem a convenção, que recusaria esse mapeamento já na construção, e serve só para a regra de
/// metadados provar que o acusa.
/// </summary>
internal static class ModeloSinteticoComColecaoInteiraDeEnumDeContrato
{
    internal static IModel Montar()
    {
        using ContextoComColecaoInteira contexto = new();

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private sealed class ContextoComColecaoInteira() : DbContext(
        new DbContextOptionsBuilder().UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x").Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<OfertaComColecaoInteira>(oferta =>
            {
                oferta.Property(o => o.Preferidas).HasConversion(new ConversorDaColecaoPelosNomes());
                oferta.Property(o => o.Eventuais).HasConversion(new ConversorDaColecaoOpcionalPelosNomes());
            });
        }
    }

    private sealed class ConversorDaColecaoPelosNomes : ValueConverter<List<ModalidadeDeOferta>, string>
    {
        public ConversorDaColecaoPelosNomes()
            : base(
                modalidades => string.Join(',', modalidades),
                texto => texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(nome => Enum.Parse<ModalidadeDeOferta>(nome)).ToList())
        {
        }
    }

    private sealed class ConversorDaColecaoOpcionalPelosNomes : ValueConverter<List<ModalidadeDeOferta?>, string>
    {
        public ConversorDaColecaoOpcionalPelosNomes()
            : base(
                modalidades => string.Join(',', modalidades),
                texto => texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(nome => (ModalidadeDeOferta?)Enum.Parse<ModalidadeDeOferta>(nome)).ToList())
        {
        }
    }
}

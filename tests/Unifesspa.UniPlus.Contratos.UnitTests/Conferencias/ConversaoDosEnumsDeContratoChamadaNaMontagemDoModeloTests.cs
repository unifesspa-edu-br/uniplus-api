namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

public sealed class ConversaoDosEnumsDeContratoChamadaNaMontagemDoModeloTests
{
    [Fact]
    public void ContextoDoPortal_EnumDeExemplo_GravaPeloCodigo()
    {
        DbContextOptions<PortalDbContext> opcoes = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase("portal-conversao").ReplaceService<IModelCustomizer, ModeloComEnumDeExemplo>().Options;
        using PortalDbContext contexto = new(opcoes);
        ConferirConversao(contexto.Model);
    }

    [Fact]
    public void ContextoDoMonolito_EnumDeExemplo_GravaPeloCodigo()
    {
        DbContextOptions<MonolitoDbContext> opcoes = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseInMemoryDatabase("monolito-conversao").ReplaceService<IModelCustomizer, ModeloComEnumDeExemplo>().Options;
        using MonolitoDbContext contexto = new(opcoes);
        ConferirConversao(contexto.Model);
    }

    [Fact]
    public void ModeloSemConvencao_EnumDeExemplo_NaoGravaPeloCodigo() =>
        PontosDoModelo.QueNaoGravamPeloCodigo(ModeloSinteticoComEnumsDeContrato.ModeloSemAConvencao())
            .Should().NotBeEmpty();

    [Fact]
    public void ModeloComConvencao_EnumDeExemplo_GravaPeloCodigo() =>
        PontosDoModelo.QueNaoGravamPeloCodigo(ModeloSinteticoComEnumsDeContrato.ModeloComAConvencao())
            .Should().BeEmpty();

    private static void ConferirConversao(IModel modelo)
    {
        IProperty propriedade = modelo.FindEntityType(typeof(ExemploDeOferta))!.FindProperty(nameof(ExemploDeOferta.Modalidade))!;
        propriedade.GetValueConverter().Should().NotBeNull();
        propriedade.GetValueConverter()!.ConvertToProvider(ModalidadeDeOferta.Presencial).Should().Be("em_sala");
    }

    public sealed class ExemploDeOferta
    {
        public Guid Id { get; set; }

        public ModalidadeDeOferta Modalidade { get; set; }
    }

    public sealed class ModeloComEnumDeExemplo(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.Entity<ExemploDeOferta>();
            base.Customize(modelBuilder, context);
        }
    }
}

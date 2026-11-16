namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Outbox.Cascading;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A corrida entre a consulta de unicidade e a gravação, contra o PostgreSQL: a segunda gravação do mesmo código
/// bate na constraint única, o handler devolve a recusa, e o <c>SaveChanges</c> que o Wolverine repete ao fim do
/// handler não pode estourar de novo, o que só vale com o rastreamento descartado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalCorridaNoBancoTests : ObrigatoriedadeLegalAdminTestBase
{
    public ObrigatoriedadeLegalCorridaNoBancoTests(CascadingFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Segunda criação do mesmo código, com a consulta prévia cega, é recusada e o save seguinte não estoura")]
    public async Task Criar_ViolacaoDeUnicidadeNoBanco_RecusaESaveSeguinteNaoEstoura()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider servicos = scope.ServiceProvider;
        IObrigatoriedadeLegalRepository repositorio = RepositorioCegoParaDuplicidade(servicos);
        IUnitOfWork unitOfWork = servicos.GetRequiredService<IUnitOfWork>();
        CriarObrigatoriedadeLegalCommand comando = ComandoDeCriar(UniqueRegraCodigo());

        Result<Guid> primeira = await Criar(comando, repositorio, servicos, unitOfWork);
        Result<Guid> segunda = await Criar(comando, repositorio, servicos, unitOfWork);

        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        segunda.Error!.Code.Should().BeOneOf("ObrigatoriedadeLegal.RegraCodigoDuplicada", "ObrigatoriedadeLegal.HashColisao");
        Func<Task> saveDoWolverine = () => unitOfWork.SalvarAlteracoesAsync(CancellationToken.None);
        await saveDoWolverine.Should().NotThrowAsync("o rastreamento da regra recusada foi descartado");
    }

    private static Task<Result<Guid>> Criar(
        CriarObrigatoriedadeLegalCommand comando,
        IObrigatoriedadeLegalRepository repositorio,
        IServiceProvider servicos,
        IUnitOfWork unitOfWork) =>
        CriarObrigatoriedadeLegalCommandHandler.Handle(
            comando,
            repositorio,
            servicos.GetRequiredService<ConferenciaDaObrigatoriedadeLegal>(),
            unitOfWork,
            CancellationToken.None);

    // A janela da corrida: a consulta prévia diz "livre" e só a constraint do banco enxerga a duplicata.
    private static IObrigatoriedadeLegalRepository RepositorioCegoParaDuplicidade(IServiceProvider servicos)
    {
        IObrigatoriedadeLegalRepository real = servicos.GetRequiredService<IObrigatoriedadeLegalRepository>();
        IObrigatoriedadeLegalRepository cego = Substitute.For<IObrigatoriedadeLegalRepository>();
        cego.ExisteRegraCodigoAtivoAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        cego.AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>())
            .Returns(chamada => real.AdicionarAsync(chamada.Arg<ObrigatoriedadeLegal>(), chamada.Arg<CancellationToken>()));
        return cego;
    }

    private static CriarObrigatoriedadeLegalCommand ComandoDeCriar(string regraCodigo) =>
        new(
            "*",
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 6, 1),
            null,
            null,
            null);
}

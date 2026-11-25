namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Outbox.Cascading;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

    [Theory(DisplayName = "Segunda criação que perde a corrida é recusada pela constraint violada e o save seguinte não estoura")]
    [InlineData(2026, 6, 1, "ObrigatoriedadeLegal.HashColisao")]
    [InlineData(2026, 7, 1, "ObrigatoriedadeLegal.RegraCodigoDuplicada")]
    public async Task Criar_ViolacaoDeUnicidadeNoBanco_RecusaESaveSeguinteNaoEstoura(int ano, int mes, int dia, string codigoEsperado)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider servicos = scope.ServiceProvider;
        IObrigatoriedadeLegalRepository repositorio = RepositorioCegoParaDuplicidade(servicos);
        IUnitOfWork unitOfWork = servicos.GetRequiredService<IUnitOfWork>();
        string regraCodigo = UniqueRegraCodigo();
        (await Criar(ComandoDeCriar(regraCodigo), repositorio, servicos, unitOfWork)).IsSuccess.Should().BeTrue();

        Result<Guid> segunda = await Criar(
            ComandoDeCriar(regraCodigo) with { VigenciaInicio = new DateOnly(ano, mes, dia) }, repositorio, servicos, unitOfWork);

        segunda.Error!.Code.Should().Be(codigoEsperado);
        await ConferirSaveSeguinteAsync(unitOfWork);
    }

    [Theory(DisplayName = "Atualização que perde a corrida é recusada pela constraint violada e o save seguinte não estoura")]
    [InlineData(2026, 6, 1, "ObrigatoriedadeLegal.HashColisao")]
    [InlineData(2026, 7, 1, "ObrigatoriedadeLegal.RegraCodigoDuplicada")]
    public async Task Atualizar_ViolacaoDeUnicidadeNoBanco_RecusaESaveSeguinteNaoEstoura(int ano, int mes, int dia, string codigoEsperado)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider servicos = scope.ServiceProvider;
        IObrigatoriedadeLegalRepository repositorio = RepositorioCegoParaDuplicidade(servicos);
        IUnitOfWork unitOfWork = servicos.GetRequiredService<IUnitOfWork>();
        string codigoEmUso = UniqueRegraCodigo();
        await Criar(ComandoDeCriar(codigoEmUso), repositorio, servicos, unitOfWork);
        Guid idDestaRegra = (await Criar(ComandoDeCriar(UniqueRegraCodigo()), repositorio, servicos, unitOfWork)).Value;
        AtualizarObrigatoriedadeLegalCommand comando = ComandoDeAtualizar(idDestaRegra, codigoEmUso) with { VigenciaInicio = new DateOnly(ano, mes, dia) };

        Result resultado = await Atualizar(comando, repositorio, servicos, unitOfWork);

        resultado.Error!.Code.Should().Be(codigoEsperado);
        await ConferirSaveSeguinteAsync(unitOfWork);
    }

    private static async Task ConferirSaveSeguinteAsync(IUnitOfWork unitOfWork)
    {
        Func<Task> saveDoWolverine = () => unitOfWork.SalvarAlteracoesAsync(CancellationToken.None);
        await saveDoWolverine.Should().NotThrowAsync("o rastreamento da regra recusada foi descartado");
    }

    private static Task<Result> Atualizar(
        AtualizarObrigatoriedadeLegalCommand comando,
        IObrigatoriedadeLegalRepository repositorio,
        IServiceProvider servicos,
        IUnitOfWork unitOfWork) =>
        AtualizarObrigatoriedadeLegalCommandHandler.Handle(
            comando,
            repositorio,
            servicos.GetRequiredService<ConferenciaDaObrigatoriedadeLegal>(),
            unitOfWork,
            CancellationToken.None);

    private static AtualizarObrigatoriedadeLegalCommand ComandoDeAtualizar(Guid id, string regraCodigo) =>
        new(
            id,
            TodosOsTiposDeProcesso.Codigo,
            CategoriaObrigatoriedade.Outros.Codigo(),
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 6, 1),
            null,
            null,
            null);

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
        cego.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(chamada => real.ObterPorIdAsync(chamada.Arg<Guid>(), chamada.Arg<CancellationToken>()));
        cego.AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>())
            .Returns(chamada => real.AdicionarAsync(chamada.Arg<ObrigatoriedadeLegal>(), chamada.Arg<CancellationToken>()));
        return cego;
    }

    private static CriarObrigatoriedadeLegalCommand ComandoDeCriar(string regraCodigo) =>
        new(
            TodosOsTiposDeProcesso.Codigo,
            CategoriaObrigatoriedade.Outros.Codigo(),
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 6, 1),
            null,
            null,
            null);
}

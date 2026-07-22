namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PrecedenciasFase;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// Cobertura de concorrência: duas arestas distintas e mutuamente
/// consistentes isoladamente (A→B e, em paralelo, B→A) não podem, juntas,
/// fechar um ciclo — mesmo quando cada uma vê o grafo sem a aresta da outra
/// no instante da leitura. O <c>pg_advisory_xact_lock</c> em
/// <c>TravarGrafoParaEscritaAsync</c> serializa as duas escritas: a ordem de
/// conclusão não é determinística, mas exatamente uma persiste e o grafo
/// final permanece acíclico.
/// </summary>
/// <remarks>
/// A janela da corrida real (leitura→INSERT) é curta demais para falhar de
/// forma confiável de ponta a ponta via HTTP/Wolverine sem um hook de teste
/// no handler — por isso <see cref="TravarGrafoParaEscritaAsync_BloqueiaSegundaTransacaoAteAPrimeiraLiberar"/>
/// prova o mecanismo (o advisory lock bloqueia de fato) de forma
/// determinística, e <see cref="DuasArestasInversasConcorrentes_NuncaFormamCiclo"/>
/// cobre o resultado observável de ponta a ponta como rede de segurança.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrecedenciaFaseConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public PrecedenciaFaseConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Duas arestas inversas concorrentes (A→B e B→A) nunca formam um ciclo persistido")]
    public async Task DuasArestasInversasConcorrentes_NuncaFormamCiclo()
    {
        MonolitoApiFactory api = _fixture.Factory;

        var comandoIda = new CriarPrecedenciaFaseCommand("ENSALAMENTO", "AVALIACAO");
        var comandoVolta = new CriarPrecedenciaFaseCommand("AVALIACAO", "ENSALAMENTO");

        // Cada lado da corrida usa seu próprio scope/IMessageBus — mesmo
        // padrão de PublicacaoConcorrenciaTests (Selecao): os dois scopes só
        // são descartados no fim do método.
        await using AsyncServiceScope idaScope = api.Services.CreateAsyncScope();
        await using AsyncServiceScope voltaScope = api.Services.CreateAsyncScope();
        IMessageBus idaBus = idaScope.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus voltaBus = voltaScope.ServiceProvider.GetRequiredService<IMessageBus>();

        Task<Result<Guid>> idaTask = idaBus.InvokeAsync<Result<Guid>>(comandoIda);
        Task<Result<Guid>> voltaTask = voltaBus.InvokeAsync<Result<Guid>>(comandoVolta);

        await Task.WhenAll(idaTask, voltaTask);

        Result<Guid> idaResultado = await idaTask;
        Result<Guid> voltaResultado = await voltaTask;

        // O lock serializa — não paraleliza de verdade — então exatamente um
        // dos dois lados vence: o primeiro a adquirir o advisory lock lê o
        // grafo ainda sem a aresta do outro e persiste; o segundo, ao
        // adquirir o lock em seguida, já lê o grafo COM a aresta do
        // primeiro e a guarda de ciclo do domínio recusa a sua.
        (idaResultado.IsSuccess, voltaResultado.IsSuccess).Should().BeOneOf(
            (true, false), (false, true));

        Result<Guid> perdedor = idaResultado.IsFailure ? idaResultado : voltaResultado;
        perdedor.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.CicloDetectado);

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        List<PrecedenciaFase> arestasEnvolvidas = await readDb.PrecedenciasFase
            .AsNoTracking()
            .Where(a =>
                (a.AntecessoraCodigo == "ENSALAMENTO" && a.SucessoraCodigo == "AVALIACAO")
                || (a.AntecessoraCodigo == "AVALIACAO" && a.SucessoraCodigo == "ENSALAMENTO"))
            .ToListAsync();

        arestasEnvolvidas.Should().ContainSingle(
            "o grafo final não pode conter as duas arestas — isso seria um ciclo de dois nós");
    }

    [Fact(DisplayName =
        "TravarGrafoParaEscritaAsync bloqueia uma segunda transação até a primeira liberar")]
    public async Task TravarGrafoParaEscritaAsync_BloqueiaSegundaTransacaoAteAPrimeiraLiberar()
    {
        MonolitoApiFactory api = _fixture.Factory;

        await using AsyncServiceScope primeiraScope = api.Services.CreateAsyncScope();
        await using AsyncServiceScope segundaScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext primeiraDb = primeiraScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        IPrecedenciaFaseRepository primeiroRepositorio =
            primeiraScope.ServiceProvider.GetRequiredService<IPrecedenciaFaseRepository>();
        IPrecedenciaFaseRepository segundoRepositorio =
            segundaScope.ServiceProvider.GetRequiredService<IPrecedenciaFaseRepository>();
        ConfiguracaoDbContext segundaDb = segundaScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        await using IDbContextTransaction primeiraTx =
            await primeiraDb.Database.BeginTransactionAsync();
        await primeiroRepositorio.TravarGrafoParaEscritaAsync(CancellationToken.None);

        Task segundaTarefa = Task.Run(async () =>
        {
            await using IDbContextTransaction segundaTx =
                await segundaDb.Database.BeginTransactionAsync();
            await segundoRepositorio.TravarGrafoParaEscritaAsync(CancellationToken.None);
            await segundaTx.CommitAsync();
        });

        // A segunda transação bloqueia no advisory lock enquanto a primeira
        // não commita — sem isso, a corrida de P1-02 não teria como ser
        // serializada. Um curto prazo é suficiente para observar o bloqueio
        // sem tornar o teste sensível a variação normal de agendamento.
        Task tarefaConcluidaCedo = await Task.WhenAny(segundaTarefa, Task.Delay(TimeSpan.FromMilliseconds(500)));
        tarefaConcluidaCedo.Should().NotBe(
            segundaTarefa,
            "a segunda transação deveria estar bloqueada aguardando o lock da primeira");

        await primeiraTx.CommitAsync();

        await segundaTarefa.WaitAsync(TimeSpan.FromSeconds(10));
        segundaTarefa.IsCompletedSuccessfully.Should().BeTrue(
            "liberado o lock da primeira transação, a segunda deve concluir rapidamente");
    }
}

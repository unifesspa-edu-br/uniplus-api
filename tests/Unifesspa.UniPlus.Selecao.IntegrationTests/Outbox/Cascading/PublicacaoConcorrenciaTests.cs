namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Wolverine;

using Application.Commands.ProcessosSeletivos;
using Domain.Entities;
using Domain.Enums;
using Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de concorrência (revisão do PR #791, achado P1): dois handlers
/// que carregam o MESMO processo via <c>ObterComConfiguracaoAsync</c> — um
/// <c>DefinirEtapas</c> e um <c>Publicar</c> — não podem produzir um estado
/// em que o processo está Publicado mas o snapshot congelado diverge da
/// configuração viva. O lock pessimista (<c>SELECT ... FOR UPDATE</c> em
/// <c>ProcessoSeletivoRepository.ObterComConfiguracaoAsync</c>) serializa os
/// dois handlers — a ordem de conclusão não é determinística, mas o
/// resultado final sempre é consistente numa das duas formas válidas.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class PublicacaoConcorrenciaTests
{
    private readonly CascadingFixture _fixture;

    public PublicacaoConcorrenciaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "DefinirEtapas concorrente com Publicar nunca deixa snapshot desatualizado com processo publicado (RN08/CA-04)")]
    public async Task DefinirEtapasConcorrenteComPublicar_NuncaDeixaEstadoInconsistente()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient _ = api.CreateClient();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope seedScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(seedDb, $"Concorrência {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        var definirEtapasCommand = new DefinirEtapasCommand(
            processoId,
            [new EtapaProcessoInput("Prova Discursiva (revisada)", CaraterEtapa.Classificatoria, Peso: 1m, NotaMinima: null, Ordem: 1)], PrecondicaoIfMatch.Ausente);
        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
            DocumentoEditalId: documentoId,
            Ato: DadosDoAtoDeTeste.Padrao);

        // Cada lado da corrida usa seu próprio scope/IMessageBus — os dois
        // scopes só são descartados no fim do método (após ambas as tasks
        // serem aguardadas), sem indireção via helper genérico que faça o
        // analisador CA2025 perder o rastro do ciclo de vida do
        // IAsyncDisposable em relação à Task que ele origina.
        await using AsyncServiceScope definirEtapasScope = api.Services.CreateAsyncScope();
        await using AsyncServiceScope publicarScope = api.Services.CreateAsyncScope();
        IMessageBus definirEtapasBus = definirEtapasScope.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus publicarBus = publicarScope.ServiceProvider.GetRequiredService<IMessageBus>();

        // Os Definir* passaram a devolver o ETag da sessão editorial (ADR-0110 D5): o tipo de
        // retorno é Result<MutacaoAceita>. Pedir Result nu ao bus devolveria null, e a corrida
        // "provaria" o que quer que o NullReferenceException interrompesse primeiro.
        Task<Result<MutacaoAceita>> definirEtapasTask = definirEtapasBus.InvokeAsync<Result<MutacaoAceita>>(definirEtapasCommand);
        Task<Result> publicarTask = publicarBus.InvokeAsync<Result>(publicarCommand);

        await Task.WhenAll(definirEtapasTask, publicarTask);

        Result<MutacaoAceita> definirEtapasResultado = await definirEtapasTask;
        Result publicarResultado = await publicarTask;

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processoFinal = await readDb.ProcessosSeletivos
            .AsNoTracking()
            .Include(p => p.Etapas)
            .FirstAsync(p => p.Id == processoId);

        if (definirEtapasResultado.IsFailure)
        {
            // Publicar venceu a corrida: DefinirEtapas viu Status=Publicado
            // (só possível porque o lock forçou seu load a esperar o commit
            // da publicação) e foi bloqueado — nunca uma mutação stale.
            definirEtapasResultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
            publicarResultado.IsSuccess.Should().BeTrue(publicarResultado.Error?.Message);
            processoFinal.Status.Should().Be(StatusProcesso.Publicado);
            processoFinal.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva",
                "a etapa original semeada — DefinirEtapas nunca commitou");
        }
        else
        {
            // DefinirEtapas venceu a corrida (rodou e commitou antes do lock
            // de Publicar): Publicar, ao carregar DEPOIS, vê a config nova e
            // o snapshot já nasce consistente com ela — sem publicação com
            // dado desatualizado.
            processoFinal.Etapas.Should().ContainSingle(e => e.Nome == "Prova Discursiva (revisada)");
            publicarResultado.IsSuccess.Should().BeTrue(publicarResultado.Error?.Message);
            processoFinal.Status.Should().Be(StatusProcesso.Publicado);

            // Filtra pela versão DESTE processo — sob carga de suíte completa, outros
            // testes do mesmo Collection publicam seus próprios processos em paralelo
            // (com a etapa padrão do seeder, "Prova Objetiva"); um FirstAsync() sem
            // filtro pega o snapshot de QUALQUER um deles, não necessariamente o desta
            // execução (achado ao investigar flake sob `dotnet test` da suíte inteira).
            VersaoConfiguracao versao = await readDb.VersoesConfiguracao
                .AsNoTracking()
                .SingleAsync(v => v.ProcessoSeletivoId == processoId);
            JsonNode configuracao = JsonNode.Parse(versao.ConfiguracaoCongelada)!;
            JsonArray etapasNoSnapshot = configuracao["etapas"]!.AsArray();
            etapasNoSnapshot.Should().ContainSingle();
            etapasNoSnapshot[0]!["nome"]!.GetValue<string>().Should().Be(
                "Prova Discursiva (revisada)",
                "o snapshot deve refletir a configuração vigente no momento em que Publicar efetivamente carregou o agregado, nunca uma versão anterior");
        }
    }
}

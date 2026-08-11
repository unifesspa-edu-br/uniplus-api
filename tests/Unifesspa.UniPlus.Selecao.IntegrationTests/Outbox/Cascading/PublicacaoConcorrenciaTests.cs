namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

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

        // origemId real do seed determinístico de TipoEtapa (CriaCadastroTiposEtapa) — este
        // teste passa pelo bus real, então ITipoEtapaReader resolve contra o Postgres do
        // Testcontainers, não um mock.
        var definirEtapasCommand = new DefinirEtapasCommand(
            processoId,
            [new EtapaProcessoInput(
                "Prova Discursiva (revisada)", CaraterEtapa.Classificatoria,
                TipoEtapaOrigemId: new Guid("019fee1e-7000-7000-8000-000000000001"),
                Peso: 1m, NotaMinima: null, Ordem: 1)], PrecondicaoIfMatch.Ausente);
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

    /// <summary>
    /// Story #575 — mesma corrida acima, agora entre <c>DefinirCascataRemanejamentoCommand</c>
    /// e <c>PublicarProcessoSeletivoCommand</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Diferença deliberada do teste acima: lá, <c>DefinirEtapasCommand</c> não é
    /// pré-requisito de <c>Publicar</c> — o processo semeado já é publicável de partida, e
    /// só <c>DefinirEtapas</c> pode perder a corrida. Aqui a assimetria é inversa: o
    /// processo é semeado com a oferta federal completa (8 modalidades <c>SegueCascata</c>)
    /// mas <b>sem</b> cascata — <c>PendenciaDaCascata</c> a exige, então <c>Publicar</c> só
    /// sucede se <c>DefinirCascataRemanejamento</c> já tiver comitado. Só dois desfechos são
    /// alcançáveis — nunca um terceiro em que <c>Publicar</c> sucede sem a cascata:
    /// </para>
    /// <list type="bullet">
    ///   <item>DefinirCascataRemanejamento comita ANTES de Publicar carregar: os dois
    ///     sucedem, e o snapshot nasce com a cascata (não há como Publicar rodar sem
    ///     tê-la já commitada);</item>
    ///   <item>Publicar carrega e recusa (CascataOrigemAusente) ANTES de
    ///     DefinirCascataRemanejamento comitar: Publicar falha (Status permanece Rascunho,
    ///     SaveChanges nunca roda), e DefinirCascataRemanejamento, ao carregar depois, ainda
    ///     vê Status=Rascunho — nunca bloqueado por MutacaoPosPublicacaoBloqueada — e
    ///     sucede.</item>
    /// </list>
    /// <para>
    /// Em nenhum dos dois um snapshot é publicado SEM a cascata que a oferta exige — a
    /// prova de ausência de divergência, não "exatamente uma operação vence". <b>Achado de
    /// revisão:</b> ao contrário do teste-irmão acima (onde o <c>SELECT ... FOR UPDATE</c> de
    /// <c>ObterParaMutacaoAsync</c> é o que impede um lost update em <c>Etapas</c>, campo que
    /// os dois lados escrevem), aqui a invariante não depende do lock: <c>Publicar</c> nunca
    /// escreve na tabela da cascata, e a checagem de <c>PendenciaDaCascata</c> roda sobre o
    /// que a própria transação de <c>Publicar</c> leu — um <c>Publicar</c> sem a cascata
    /// commitada sempre falha por construção, com ou sem lock. O <c>FOR UPDATE</c> ainda
    /// importa para outras invariantes do agregado (a etapa acima, por exemplo); só não é o
    /// que este teste especificamente exercita.
    /// </para>
    /// </remarks>
    [Fact(DisplayName =
        "DefinirCascataRemanejamento concorrente com Publicar nunca publica snapshot sem a cascata que a oferta federal exige (Story #575, RN08/CA-04)")]
    public async Task DefinirCascataRemanejamentoConcorrenteComPublicar_NuncaPublicaSnapshotSemCascata()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient _ = api.CreateClient();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope seedScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (Guid id, Guid docId) = await SemearProcessoFederalSemCascataAsync(
                seedDb, $"Concorrência Cascata {Guid.CreateVersion7()}");
            processoId = id;
            documentoId = docId;
        }

        var definirCascataCommand = new DefinirCascataRemanejamentoCommand(
            processoId,
            RegraCodigo: "REMANEJ-CASCATA-LEI-12711",
            RegraVersao: "v1",
            FallbackCodigo: "AC",
            Destinos: MatrizLegalCompletaComoInput(),
            Precondicao: PrecondicaoIfMatch.Ausente);
        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
            DocumentoEditalId: documentoId,
            Ato: DadosDoAtoDeTeste.Padrao);

        // Mesmo raciocínio de DefinirEtapasConcorrenteComPublicar_NuncaDeixaEstadoInconsistente
        // acima: cada lado da corrida usa seu próprio scope/IMessageBus.
        await using AsyncServiceScope definirScope = api.Services.CreateAsyncScope();
        await using AsyncServiceScope publicarScope = api.Services.CreateAsyncScope();
        IMessageBus definirBus = definirScope.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus publicarBus = publicarScope.ServiceProvider.GetRequiredService<IMessageBus>();

        Task<Result<MutacaoAceita>> definirTask = definirBus.InvokeAsync<Result<MutacaoAceita>>(definirCascataCommand);
        Task<Result> publicarTask = publicarBus.InvokeAsync<Result>(publicarCommand);

        await Task.WhenAll(definirTask, publicarTask);

        Result<MutacaoAceita> definirResultado = await definirTask;
        Result publicarResultado = await publicarTask;

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processoFinal = await readDb.ProcessosSeletivos
            .AsNoTracking()
            .Include(p => p.Cascata!).ThenInclude(c => c.Destinos)
            .FirstAsync(p => p.Id == processoId);

        // DefinirCascataRemanejamento nunca é bloqueado NESTE desenho — ele só falharia
        // com MutacaoPosPublicacaoBloqueada, e isso exigiria Publicar já ter sucedido, o
        // que por sua vez exige a cascata já commitada, contradição. É a invariante que
        // este teste prova por construção; um FAIL aqui denunciaria o terceiro desfecho
        // impossível (Publicar sucede sem cascata).
        definirResultado.IsSuccess.Should().BeTrue(definirResultado.Error?.Message);
        processoFinal.Cascata.Should().NotBeNull();
        processoFinal.Cascata!.Destinos.Should().HaveCount(56, "a matriz legal completa (8×7) sempre acaba definida, ganhando ou perdendo a corrida");

        if (publicarResultado.IsFailure)
        {
            // Publicar carregou ANTES de DefinirCascataRemanejamento comitar: recusado
            // nomeadamente, sem publicar snapshot incompleto.
            publicarResultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
            processoFinal.Status.Should().Be(StatusProcesso.Rascunho,
                "Publicar falhou ANTES de qualquer SaveChanges — a transição de status nunca aconteceu");

            bool existeVersaoPublicada = await readDb.VersoesConfiguracao
                .AsNoTracking().AnyAsync(v => v.ProcessoSeletivoId == processoId);
            existeVersaoPublicada.Should().BeFalse("Publicar recusado não pode deixar uma versão congelada para trás");
        }
        else
        {
            // DefinirCascataRemanejamento venceu a corrida: Publicar, ao carregar DEPOIS,
            // já viu a cascata — o snapshot nasce consistente com ela.
            processoFinal.Status.Should().Be(StatusProcesso.Publicado);

            // Filtra pela versão DESTE processo — mesmo raciocínio do teste acima: sob carga
            // de suíte completa, outros testes do mesmo Collection publicam em paralelo.
            VersaoConfiguracao versao = await readDb.VersoesConfiguracao
                .AsNoTracking()
                .SingleAsync(v => v.ProcessoSeletivoId == processoId);
            JsonNode configuracao = JsonNode.Parse(versao.ConfiguracaoCongelada)!;
            JsonObject cascataNoSnapshot = configuracao["cascataRemanejamento"]!.AsObject();
            cascataNoSnapshot["presente"]!.GetValue<bool>().Should().BeTrue(
                "o snapshot deve refletir a cascata vigente no momento em que Publicar efetivamente carregou o " +
                "agregado, nunca uma publicação que ignora a cascata recém-definida");
            cascataNoSnapshot["ordens"]!.AsArray().Should().HaveCount(8, "as 8 origens federais da matriz legal completa");
        }
    }

    /// <summary>
    /// A oferta federal completa (Lei 12.711), SEM a cascata que ela exige —
    /// <c>ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome,
    /// comCascata: false)</c> — mais um <see cref="DocumentoEdital"/> já confirmado, no
    /// mesmo padrão de <see cref="ProcessoSeletivoPublicavelSeeder.SemearAsync"/>.
    /// </summary>
    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoFederalSemCascataAsync(
        SelecaoDbContext db, string nome)
    {
        ProcessoSeletivo processo = Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos
            .ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome, comCascata: false);
        await db.ProcessosSeletivos.AddAsync(processo);

        string hashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo.Id, documento.Id);
    }

    /// <summary>
    /// A matriz legal completa (8×7), fallback AC, como <c>DestinoRemanejamentoInput</c> —
    /// mesma forma exigida pelo <c>esquema_args</c> congelado de <c>REMANEJ-CASCATA-LEI-12711
    /// v1</c> (RN-CASCATA-5, validada pelo handler): cada origem lista as outras 7 federais,
    /// na ordem de <see cref="ModalidadesFederaisLei12711.Codigos"/>.
    /// </summary>
    private static List<DestinoRemanejamentoInput> MatrizLegalCompletaComoInput()
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamentoInput> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(new DestinoRemanejamentoInput(origem, i + 1, destinosDaOrigem[i]));
            }
        }

        return destinos;
    }
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeRetificacaoPersistida;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeVersaoVigentePersistida;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do seletor de configuração vigente
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0075/0076/0104):
/// <c>ObterVersaoVigenteAsync</c> resolve a <see cref="VersaoConfiguracao"/> de maior
/// <c>vigente_a_partir_de</c> ≤ o instante, desempatando por <c>numero_versao</c> — sem ler atributo
/// algum do ato que a criou.
/// </summary>
/// <remarks>
/// A data DOCUMENTAL (o que o ato declara, e que a retificação republica) não decide nada; quem
/// ordena é a vigência, do relógio do sistema, monotônica por construção.
/// </remarks>
[Collection(VersaoVigentePersistenciaColecao.Nome)]
public sealed class VersaoVigentePorInstantePersistenciaTests
{
    private readonly CenarioDeVersaoVigentePersistida _cenario;

    public VersaoVigentePorInstantePersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeVersaoVigentePersistida(fixture);
    }

    [Fact(DisplayName = "Seletor resolve a versão de maior vigência ≤ instante — e nada antes da primeira publicação (ADR-0076)")]
    public async Task ObterVersaoVigente_ResolveMaiorVigenciaAntesOuIgualAoInstante()
    {
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, VersaoConfiguracao v2) = await _cenario.PublicarERetificarAsync(
            nameof(ObterVersaoVigente_ResolveMaiorVigenciaAntesOuIgualAoInstante), clock, TimeSpan.FromDays(1));

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        // Instante posterior à retificação → a versão 2.
        VersaoConfiguracao? posterior = await repository
            .ObterVersaoVigenteAsync(processoId, T0.AddDays(2), CancellationToken.None);
        posterior.Should().NotBeNull();
        posterior!.Id.Should().Be(v2.Id);
        posterior.NumeroVersao.Should().Be(2);

        // Instante exatamente em T0 → a versão 1 (a 2, vigente em T0+1d, é excluída).
        VersaoConfiguracao? emT0 = await repository
            .ObterVersaoVigenteAsync(processoId, T0, CancellationToken.None);
        emT0.Should().NotBeNull();
        emT0!.Id.Should().Be(v1.Id);
        emT0.HashConfiguracao.Should().Be(v1.HashConfiguracao);

        // Antes da primeira publicação → vazio, sem recorrer silenciosamente a nada.
        VersaoConfiguracao? antes = await repository
            .ObterVersaoVigenteAsync(processoId, T0.AddSeconds(-1), CancellationToken.None);
        antes.Should().BeNull("antes da primeira publicação não há configuração vigente — a ausência aflora (ADR-0076)");
    }

    [Fact(DisplayName = "Seletor normaliza instante em offset não-UTC — Npgsql exige UTC em timestamptz")]
    public async Task ObterVersaoVigente_InstanteOffsetNaoUtc_ResolveOMesmo()
    {
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, _) = await _cenario.PublicarERetificarAsync(
            nameof(ObterVersaoVigente_InstanteOffsetNaoUtc_ResolveOMesmo), clock, TimeSpan.FromDays(1));

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        // O MESMO instante de T0, expresso com offset -03:00 (ex.: um RFC 3339
        // vindo da query string) resolve a mesma versão.
        VersaoConfiguracao? vigente = await repository
            .ObterVersaoVigenteAsync(processoId, T0.ToOffset(TimeSpan.FromHours(-3)), CancellationToken.None);

        vigente.Should().NotBeNull();
        vigente!.Id.Should().Be(v1.Id);
    }

    [Fact(DisplayName = "Relógio regredido: a vigência da sucessora é ancorada na anterior — a cadeia não anda para trás")]
    public async Task ObterVersaoVigente_RelogioRegredido_ResolvePelaVigenciaAncorada()
    {
        // O relógio do host regride entre a abertura e a retificação (ajuste NTP em degrau,
        // troca de hora). Como é a VIGÊNCIA que ordena as versões, um retrocesso faria a
        // versão 1 voltar a ser a vigente depois de a 2 existir. VersaoConfiguracao.Suceder
        // ancora a sucessora no instante da anterior: as duas passam a viger em T0, e o
        // desempate cabe ao número da versão.
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, VersaoConfiguracao v2) = await _cenario.PublicarERetificarAsync(
            nameof(ObterVersaoVigente_RelogioRegredido_ResolvePelaVigenciaAncorada),
            clock,
            avancoAteRetificacao: TimeSpan.FromHours(-1));

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        v2.VigenteAPartirDe.Should().Be(v1.VigenteAPartirDe, "a vigência da sucessora é ancorada — nunca regride");

        // Antes de T0 nada vige: nenhuma versão passou a valer ainda.
        VersaoConfiguracao? antesDaVigencia = await repository
            .ObterVersaoVigenteAsync(processoId, T0.AddMinutes(-30), CancellationToken.None);
        antesDaVigencia.Should().BeNull("um relógio que regrediu não faz a configuração vigir antes");

        // A partir de T0 as duas vigem no mesmo instante — e o desempate por número elege a
        // retificação, a mais nova.
        VersaoConfiguracao? vigente = await repository
            .ObterVersaoVigenteAsync(processoId, T0, CancellationToken.None);
        vigente.Should().NotBeNull();
        vigente!.Id.Should().Be(v2.Id, "empate de instante desempata pelo maior numero_versao");
        vigente.NumeroVersao.Should().Be(2);
    }

    [Fact(DisplayName = "A versão criada por um ato resolve NO INSTANTE do próprio ato — relógio de parede, sem relógio manual (ADR-0075)")]
    public async Task ObterVersaoVigente_NoInstanteDoProprioAto_ResolveAVersaoQueEleCriou()
    {
        // O instante que o ProcessoPublicadoEvent publica é o do ATO
        // (Edital.DataDoAto). Um consumidor que reavalie o ato contra a
        // configuração vigente naquele instante — que é o que a ADR-0075 manda
        // fazer — precisa encontrar a versão que o próprio ato criou.
        //
        // Com o relógio REAL (não o manual dos demais testes), duas leituras de
        // GetUtcNow() dentro de Publicar deixariam VigenteAPartirDe alguns ticks à
        // frente de DataDoAto, e a consulta no instante do ato não acharia
        // nada. A raiz lê o relógio uma única vez — este teste é o que trava isso.
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_NoInstanteDoProprioAto_ResolveAVersaoQueEleCriou));

        // O evento publica OccurredOn = VigenteAPartirDe (o instante do SISTEMA), e não a data
        // que o documento declara — esta é informada pelo operador e pode ser retroativa. É por
        // esse instante que um consumidor resolve a configuração vigente do ato (ADR-0075).
        VersaoConfiguracao versao = await _cenario.PublicarEPersistirAsync(processo, TimeProvider.System);

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        // O instante do ato, tal como o evento o publica.
        VersaoConfiguracao? noInstanteDoAto = await leitura
            .ObterVersaoVigenteAsync(processo.Id, versao.VigenteAPartirDe, CancellationToken.None);

        noInstanteDoAto.Should().NotBeNull("o ato deve resolver a configuração que ele próprio congelou");
        noInstanteDoAto!.Id.Should().Be(versao.Id);
    }

    [Fact(DisplayName = "Relógio do host atrás da vigência: a consulta corrente aflora a ausência (422) — nunca devolve versão que ainda não vigora")]
    public async Task Handle_RelogioCorrenteAtrasDaVigencia_AfloraVigenteAusente()
    {
        // Continuação do cenário de relógio regredido, agora pelo handler: enquanto o relógio do
        // host permanecer atrás da vigência ancorada, o "agora" precede toda versão
        // e a consulta corrente não resolve nada.
        //
        // É deliberado: o seletor anterior também exigia data ≤ instante, e um host com relógio
        // atrasado já caía nesta janela. Um "agora" artificialmente monotônico devolveria uma
        // configuração como vigente contra um presente que o próprio sistema não reconhece —
        // mascarando o relógio errado em vez de deixá-lo aflorar (ADR-0076). O
        // certame não perde nada: a consulta com instante explícito resolve, e o
        // 422 desaparece assim que o relógio se recupera.
        RelogioManualDaVigencia clock = new(T0);
        (Guid processoId, _, _) = await _cenario.PublicarERetificarAsync(
            nameof(Handle_RelogioCorrenteAtrasDaVigencia_AfloraVigenteAusente), clock, TimeSpan.FromHours(-1));

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        // O relógio do host segue regredido (T0 − 1h) e o cliente não informa instante.
        Result<Application.DTOs.SnapshotVigenteDto> corrente = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            clock,
            CancellationToken.None);

        corrente.IsFailure.Should().BeTrue();
        corrente.Error!.Code.Should().Be("Snapshot.VigenteAusente");

        // Com o instante em que a configuração de fato passou a viger, resolve.
        Result<Application.DTOs.SnapshotVigenteDto> emT0 = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, T0),
            repository,
            clock,
            CancellationToken.None);

        emT0.IsSuccess.Should().BeTrue();
        // O vigente em T0 é a versão do topo — a que a retificação criou. Não há campo de
        // natureza a conferir: o que a distingue é ter sido criada por um ato que emenda
        // outro, e essa relação vive no ato, em Publicações (ADR-0103).
        VersaoConfiguracao topo = await context.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstAsync(CancellationToken.None);
        topo.AtoCriadorRetificaId.Should().NotBeNull();
        emT0.Value!.AtoId.Should().Be(topo.AtoCriadorId);
        emT0.Value!.SnapshotPublicacaoId.Should().Be(topo.Id);
    }
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do seletor de
/// configuração vigente (RN08, Story #803, ADR-0075/0076/0104):
/// <c>ObterVersaoVigenteAsync</c> resolve a <see cref="VersaoConfiguracao"/> de
/// maior <c>vigente_a_partir_de</c> ≤ o instante, desempatando por
/// <c>numero_versao</c> — sem ler atributo algum do ato que a criou.
/// </summary>
/// <remarks>
/// A distinção que estes testes exercitam é a da story: a data DOCUMENTAL (o que
/// o ato declara, e que a retificação republica) não decide nada; quem ordena é a
/// vigência, do relógio do sistema, monotônica por construção.
/// </remarks>
public sealed class VersaoVigentePersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // A data que o DOCUMENTO declara (ADR-0108) — informada pelo operador, e distinta do
    // instante do sistema que rege a vigência. Aqui é fixa: o que os testes exercitam é a
    // vigência, e a data documental só precisa não se confundir com ela.

    private readonly ProcessoSeletivoDbFixture _fixture;

    public VersaoVigentePersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar[0], 64)).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU);
        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021").Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"), 1, []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static DocumentoEdital DocumentoConfirmado(Guid processoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static DadosEdital NovosDados(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), documentoId).Value!;

    /// <summary>
    /// Publica a abertura e retifica em seguida, pelo caminho de produção (a raiz
    /// do agregado), com o relógio manual que o teste controla. Devolve as duas
    /// versões da cadeia.
    /// </summary>
    private async Task<(Guid ProcessoId, VersaoConfiguracao V1, VersaoConfiguracao V2)> PublicarERetificarAsync(
        string nome,
        RelogioManual clock,
        TimeSpan avancoAteRetificacao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", clock);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(publicar.Value!, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        clock.Avancar(avancoAteRetificacao);

        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);
        VersaoConfiguracao v2;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
                carregado, dadosRetificacao, docRetificacao.HashSha256!,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição")));
            Result<VersaoConfiguracao> retificar = carregado.Retificar(
                dadosRetificacao, versaoAtual, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion,
                canonicoRetificacao.AlgoritmoHash, docRetificacao.HashSha256!, "integration-test-user",
                "Correção do prazo de inscrição", clock);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            v2 = retificar.Value!;

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(v2, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo.Id, publicar.Value!, v2);
    }

    [Fact(DisplayName = "Seletor resolve a versão de maior vigência ≤ instante — e nada antes da primeira publicação (ADR-0076)")]
    public async Task ObterVersaoVigente_ResolveMaiorVigenciaAntesOuIgualAoInstante()
    {
        RelogioManual clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, VersaoConfiguracao v2) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_ResolveMaiorVigenciaAntesOuIgualAoInstante), clock, TimeSpan.FromDays(1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

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
        RelogioManual clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, _) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_InstanteOffsetNaoUtc_ResolveOMesmo), clock, TimeSpan.FromDays(1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

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
        RelogioManual clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, VersaoConfiguracao v2) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_RelogioRegredido_ResolvePelaVigenciaAncorada),
            clock,
            avancoAteRetificacao: TimeSpan.FromHours(-1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

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
        // (Edital.DataPublicacao). Um consumidor que reavalie o ato contra a
        // configuração vigente naquele instante — que é o que a ADR-0075 manda
        // fazer — precisa encontrar a versão que o próprio ato criou.
        //
        // Com o relógio REAL (não o manual dos demais testes), duas leituras de
        // GetUtcNow() dentro de Publicar deixariam VigenteAPartirDe alguns ticks à
        // frente de DataPublicacao, e a consulta no instante do ato não acharia
        // nada. A raiz lê o relógio uma única vez — este teste é o que trava isso.
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_NoInstanteDoProprioAto_ResolveAVersaoQueEleCriou));
        DocumentoEdital documento = DocumentoConfirmado(processo.Id);
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, documento.HashSha256!));

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            documento.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        VersaoConfiguracao versao = publicar.Value!;

        // O evento publica OccurredOn = VigenteAPartirDe (o instante do SISTEMA), e não a data
        // que o documento declara — esta é informada pelo operador e pode ser retroativa. É por
        // esse instante que um consumidor resolve a configuração vigente do ato (ADR-0075).

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(context, TimeProvider.System);

        // O instante do ato, tal como o evento o publica.
        VersaoConfiguracao? noInstanteDoAto = await leitura
            .ObterVersaoVigenteAsync(processo.Id, versao.VigenteAPartirDe, CancellationToken.None);

        noInstanteDoAto.Should().NotBeNull("o ato deve resolver a configuração que ele próprio congelou");
        noInstanteDoAto!.Id.Should().Be(versao.Id);
    }

    [Fact(DisplayName = "Relógio do host atrás da vigência: a consulta corrente aflora a ausência (422) — nunca devolve versão que ainda não vigora")]
    public async Task Handle_RelogioCorrenteAtrasDaVigencia_AfloraVigenteAusente()
    {
        // Continuação do cenário acima, agora pelo handler: enquanto o relógio do
        // host permanecer atrás da vigência ancorada, o "agora" precede toda versão
        // e a consulta corrente não resolve nada.
        //
        // É deliberado, e não uma regressão desta story: o seletor anterior também
        // exigia data ≤ instante, e um host com relógio atrasado já caía nesta
        // janela. Um "agora" artificialmente monotônico devolveria uma configuração
        // como vigente contra um presente que o próprio sistema não reconhece —
        // mascarando o relógio errado em vez de deixá-lo aflorar (ADR-0076). O
        // certame não perde nada: a consulta com instante explícito resolve, e o
        // 422 desaparece assim que o relógio se recupera.
        RelogioManual clock = new(T0);
        (Guid processoId, _, _) = await PublicarERetificarAsync(
            nameof(Handle_RelogioCorrenteAtrasDaVigencia_AfloraVigenteAusente), clock, TimeSpan.FromHours(-1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

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

    [Fact(DisplayName = "O SQL do seletor toca APENAS a tabela de versões — nenhum atributo do ato entra na escolha")]
    public async Task ObterVersaoVigente_SqlSoTocaEmVersoes()
    {
        // A prova mecânica de que a vigência ordena VERSÕES, não documentos (ADR-0104). Antes,
        // este teste precisava afirmar que o SQL não citava `editais`; agora a tabela nem
        // existe, e o que resta a garantir é que nenhuma OUTRA fonte de atributo documental
        // (o documento, um join com Publicações) se infiltre no seletor pela porta dos fundos.
        RelogioManual clock = new(T0);
        (Guid processoId, _, _) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_SqlSoTocaEmVersoes), clock, TimeSpan.FromDays(1));

        CapturadorDeSql capturador = new();
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null),
                capturador)
            .Options;

        await using SelecaoDbContext context = new(options);
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        (await repository.ObterVersaoVigenteAsync(processoId, T0.AddDays(2), CancellationToken.None)).Should().NotBeNull();

        capturador.Comandos.Should().ContainSingle("a resolução do vigente é UMA leitura — não há hidratação do documento a fazer");
        string sql = capturador.Comandos[0];
        sql.Should().Contain("versoes_configuracao");
        sql.Should().NotContain("editais", "a tabela do documento não existe mais, e o seletor jamais dependeu dela");
        sql.Should().NotContain("documentos_edital", "o documento não decide qual configuração vale");
        sql.Should().NotContain("atos_normativos", "e o ato tampouco — ele entra por VALOR, não por join (ADR-0061)");
    }

    [Fact(DisplayName = "Versão cujo ato ainda não foi registrado resolve normalmente — a seleção não espera a fila drenar")]
    public async Task ObterVersaoVigente_AtoAindaNaoRegistrado_AindaResolve()
    {
        // O ato é registrado em Publicações por mensagem durável, DEPOIS do commit da
        // publicação (ADR-0108). Entre o 204 e a drenagem do outbox, a versão existe e o ato
        // ainda não — e é exatamente aí que o certame precisa continuar consultável. A
        // referência é por valor, sem FK (ADR-0061): o seletor não sabe nem pergunta se o ato
        // já existe do outro lado.
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_AtoAindaNaoRegistrado_AindaResolve));
        DocumentoEdital documento = DocumentoConfirmado(processo.Id);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, NovosDados(documento.Id), documento.HashSha256!));

        Guid atoAindaNaoRegistrado = Guid.CreateVersion7();
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            atoAindaNaoRegistrado, HashFixo, "integration-test-user", T0);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
            await writeContext.VersoesConfiguracao.AddAsync(versao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        VersaoConfiguracao? vigente = await repository
            .ObterVersaoVigenteAsync(processo.Id, T0.AddDays(1), CancellationToken.None);
        vigente.Should().NotBeNull("a seleção não depende da existência do ato do outro lado da fila");
        vigente!.Id.Should().Be(versao.Id);
        vigente.AtoCriadorId.Should().Be(atoAindaNaoRegistrado, "a referência por valor ao ato está na própria versão");
    }

    [Fact(DisplayName = "Seletor não resolve configuração de processo soft-deleted nem de processo inexistente (base do 404)")]
    public async Task ObterVersaoVigente_ProcessoInexistenteOuExcluido_RetornaNull()
    {
        RelogioManual clock = new(T0);
        (Guid processoId, _, _) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_ProcessoInexistenteOuExcluido_RetornaNull), clock, TimeSpan.FromDays(1));

        await using (SelecaoDbContext deleteContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await deleteContext.ProcessosSeletivos
                .SingleAsync(p => p.Id == processoId, CancellationToken.None);
            processo.MarkAsDeleted("integration-test-user", T0.AddDays(3));
            await deleteContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        // VersaoConfiguracao é forense (sem exclusão lógica própria), mas o EXISTS
        // através de ProcessosSeletivos herda o filtro global: um processo excluído
        // não vaza a configuração congelada.
        (await repository.ObterVersaoVigenteAsync(processoId, T0.AddDays(5), CancellationToken.None))
            .Should().BeNull("um processo excluído logicamente cai no mesmo 404 do resto da API");
        (await repository.ExisteAsync(processoId, CancellationToken.None)).Should().BeFalse();

        (await repository.ObterVersaoVigenteAsync(Guid.CreateVersion7(), T0.AddDays(5), CancellationToken.None))
            .Should().BeNull();
    }

    /// <summary>Captura o SQL emitido — prova mecânica de que o seletor não toca em <c>editais</c>.</summary>
    private sealed class CapturadorDeSql : DbCommandInterceptor
    {
        public List<string> Comandos { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            Comandos.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}

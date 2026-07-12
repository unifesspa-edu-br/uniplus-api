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
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021").Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), null, [modalidade]).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"), 1, []).Value!).IsSuccess.Should().BeTrue();
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
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(processo, dadosAbertura, docAbertura.HashSha256!);
        Result<PublicacaoResultado> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", clock);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(publicar.Value!.Versao, CancellationToken.None);
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
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(
                carregado, dadosRetificacao, docRetificacao.HashSha256!,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição"));
            Result<PublicacaoResultado> retificar = carregado.Retificar(
                dadosRetificacao, versaoAtual, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion,
                canonicoRetificacao.AlgoritmoHash, docRetificacao.HashSha256!, "integration-test-user",
                "Correção do prazo de inscrição", clock);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            v2 = retificar.Value!.Versao;

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(v2, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo.Id, publicar.Value!.Versao, v2);
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

    [Fact(DisplayName = "Relógio regredido: a retificação com data documental ANTERIOR à abertura resolve corretamente (o defeito que a story corrige)")]
    public async Task ObterVersaoVigente_RelogioRegredido_ResolvePelaVigenciaNaoPelaDataDocumental()
    {
        // Cenário do certame migrado: abertura documental às 10:00, retificação
        // documental às 09:00 (o relógio do legado regrediu). A vigência da
        // sucessora é ANCORADA na da anterior (VersaoConfiguracao.Suceder), então
        // as duas vigem a partir de 10:00 e o desempate cabe ao número da versão.
        //
        // Com o seletor antigo — maior Edital.DataPublicacao ≤ instante — este
        // certame resolvia ao contrário: entre 09:00 e 10:00 devolvia a retificação
        // (que ainda não vigia) e, a partir de 10:00, a abertura — a retificação
        // nunca era resolvida.
        RelogioManual clock = new(T0);
        (Guid processoId, VersaoConfiguracao v1, VersaoConfiguracao v2) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_RelogioRegredido_ResolvePelaVigenciaNaoPelaDataDocumental),
            clock,
            avancoAteRetificacao: TimeSpan.FromHours(-1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        // Pré-condição: a data DOCUMENTAL da retificação regrediu, a vigência não.
        Edital abertura = await context.Editais.AsNoTracking()
            .SingleAsync(e => e.ProcessoSeletivoId == processoId && e.Natureza == NaturezaEdital.Abertura, CancellationToken.None);
        Edital retificacao = await context.Editais.AsNoTracking()
            .SingleAsync(e => e.ProcessoSeletivoId == processoId && e.Natureza == NaturezaEdital.Retificacao, CancellationToken.None);
        retificacao.DataPublicacao.Should().Be(T0.AddHours(-1));
        abertura.DataPublicacao.Should().Be(T0);
        v2.VigenteAPartirDe.Should().Be(v1.VigenteAPartirDe, "a vigência da sucessora é ancorada — nunca regride");

        // Uma hora antes de T0 (quando a retificação DIZ ter sido publicada) nada
        // vige ainda: nenhuma versão passou a valer.
        VersaoConfiguracao? antesDaVigencia = await repository
            .ObterVersaoVigenteAsync(processoId, T0.AddMinutes(-30), CancellationToken.None);
        antesDaVigencia.Should().BeNull("a data que o documento declara não faz a configuração vigir");

        // A partir de T0, as duas vigem no mesmo instante — e o desempate por
        // número elege a retificação, a mais nova.
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
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(processo, dados, documento.HashSha256!);

        Result<PublicacaoResultado> publicar = processo.Publicar(
            dados, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            documento.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        Edital edital = publicar.Value!.Edital;
        VersaoConfiguracao versao = publicar.Value!.Versao;
        versao.VigenteAPartirDe.Should().Be(
            edital.DataPublicacao!.Value,
            "o ato e a versão que ele cria compartilham o instante — não são duas leituras do relógio");

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
            .ObterVersaoVigenteAsync(processo.Id, edital.DataPublicacao!.Value, CancellationToken.None);

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
        emT0.Value!.Natureza.Should().Be(nameof(NaturezaEdital.Retificacao));
    }

    [Fact(DisplayName = "Dois atos com a MESMA data documental e vigências distintas resolvem corretamente — e o banco os aceita (índice de unicidade removido)")]
    public async Task ObterVersaoVigente_MesmaDataDocumental_ResolvePelaVigencia()
    {
        // O caso real: a retificação republica a data do ato original. Enquanto
        // ux_editais_processo_data_publicacao existia, este cenário era rejeitado
        // pelo banco — a trava serializava o conjunto errado.
        //
        // As duas grandezas são atribuídas por relógios distintos de propósito: o
        // documental fica CONGELADO em D (mesma data nos dois atos) e o do sistema
        // avança (vigências distintas). Hoje as factories derivam ambas do mesmo
        // TimeProvider; o dia em que a data documental for declarada pelo usuário
        // — como o número já é —, este é o estado que chega.
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_MesmaDataDocumental_ResolvePelaVigencia));
        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);

        RelogioManual relogioDocumental = new(new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero));
        RelogioManual relogioDoSistema = new(T0);

        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);

        Edital abertura = Edital.EmitirAbertura(processo.Id, dadosAbertura, relogioDocumental.GetUtcNow()).Value!;
        Edital retificacao = Edital
            .EmitirRetificacao(processo.Id, dadosRetificacao, abertura.Id, "Correção do prazo", relogioDocumental.GetUtcNow())
            .Value!;
        retificacao.DataPublicacao.Should().Be(abertura.DataPublicacao, "pré-condição: a retificação republica a data do ato original");

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(processo, dadosAbertura, docAbertura.HashSha256!);
        VersaoConfiguracao v1 = VersaoConfiguracao.Abrir(
            processo.Id, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            abertura.Id, docAbertura.HashSha256!, "integration-test-user", relogioDoSistema.GetUtcNow());

        relogioDoSistema.Avancar(TimeSpan.FromDays(1));
        SnapshotCanonico canonicoR = Canonicalizer.Canonicalizar(
            processo, dadosRetificacao, docRetificacao.HashSha256!,
            new RetificacaoInfo(abertura.Id, "Correção do prazo"));
        VersaoConfiguracao v2 = VersaoConfiguracao.Suceder(
            v1, canonicoR.Bytes, canonicoR.SchemaVersion, canonicoR.AlgoritmoHash,
            retificacao.Id, docRetificacao.HashSha256!, abertura.Id, "integration-test-user", relogioDoSistema.GetUtcNow());

        // As duas versões vão em SaveChanges distintos, como no fluxo real (a
        // publicação e a retificação são transações separadas): o trigger de
        // sucessão valida a cadeia linha a linha, e um lote único deixaria o EF
        // livre para inserir a versão 2 antes da 1.
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddRangeAsync([docAbertura, docRetificacao], CancellationToken.None);

            // Estes dois INSERTs são a prova de que a unicidade de data_publicacao
            // caiu: com o índice, colidiriam em 23505 — os atos têm a mesma data.
            await writeContext.Editais.AddRangeAsync([abertura, retificacao], CancellationToken.None);
            await writeContext.VersoesConfiguracao.AddAsync(v1, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.VersoesConfiguracao.AddAsync(v2, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        (await repository.ObterVersaoVigenteAsync(processo.Id, T0, CancellationToken.None))!
            .Id.Should().Be(v1.Id, "em T0 só a versão 1 vige");
        (await repository.ObterVersaoVigenteAsync(processo.Id, T0.AddDays(1), CancellationToken.None))!
            .Id.Should().Be(v2.Id, "em T0+1d a versão 2 passa a viger — mesmo com a data documental idêntica");
    }

    [Fact(DisplayName = "O SQL do seletor não referencia a tabela de editais — a vigência ordena versões, não documentos")]
    public async Task ObterVersaoVigente_SqlNaoTocaEmEditais()
    {
        RelogioManual clock = new(T0);
        (Guid processoId, _, _) = await PublicarERetificarAsync(
            nameof(ObterVersaoVigente_SqlNaoTocaEmEditais), clock, TimeSpan.FromDays(1));

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

        capturador.Comandos.Should().ContainSingle();
        capturador.Comandos[0].Should().NotContain(
            "editais",
            "o seletor não consulta atributo algum do ato — é imune a tipos de ato (ADR-0104)");
        capturador.Comandos[0].Should().Contain("versoes_configuracao");
    }

    [Fact(DisplayName = "Versão órfã: o seletor a resolve mesmo sem o ato criador — a seleção não depende do documento")]
    public async Task ObterVersaoVigente_VersaoSemAtoCriador_AindaResolve()
    {
        // Estado que só um INSERT cru produz (ato_criador_id é referência por valor,
        // sem FK — ADR-0061). Serve a dois propósitos: prova que a SELEÇÃO não passa
        // pelo ato, e monta o caso em que a HIDRATAÇÃO não acha o documento.
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(ObterVersaoVigente_VersaoSemAtoCriador_AindaResolve));
        DocumentoEdital documento = DocumentoConfirmado(processo.Id);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(processo, NovosDados(documento.Id), documento.HashSha256!);

        Guid atoInexistente = Guid.CreateVersion7();
        VersaoConfiguracao orfa = VersaoConfiguracao.Abrir(
            processo.Id, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            atoInexistente, HashFixo, "integration-test-user", T0);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await writeContext.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
            await writeContext.VersoesConfiguracao.AddAsync(orfa, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        VersaoConfiguracao? vigente = await repository
            .ObterVersaoVigenteAsync(processo.Id, T0.AddDays(1), CancellationToken.None);
        vigente.Should().NotBeNull("a seleção não depende da existência do ato");
        vigente!.Id.Should().Be(orfa.Id);

        DadosDocumentaisAto? ato = await repository
            .ObterDadosDocumentaisDoAtoAsync(processo.Id, atoInexistente, CancellationToken.None);
        ato.Should().BeNull("o ato não existe — a hidratação aflora a corrupção em vez de inventar dados");
    }

    [Fact(DisplayName = "Hidratação recusa ato de OUTRO processo — sem FK, a pertença é verificada na consulta (ADR-0061)")]
    public async Task ObterDadosDocumentaisDoAto_AtoDeOutroProcesso_RetornaNull()
    {
        RelogioManual clockA = new(T0);
        (Guid processoA, VersaoConfiguracao versaoA, _) = await PublicarERetificarAsync(
            $"{nameof(ObterDadosDocumentaisDoAto_AtoDeOutroProcesso_RetornaNull)}-A", clockA, TimeSpan.FromDays(1));

        RelogioManual clockB = new(T0);
        (Guid processoB, VersaoConfiguracao versaoB, _) = await PublicarERetificarAsync(
            $"{nameof(ObterDadosDocumentaisDoAto_AtoDeOutroProcesso_RetornaNull)}-B", clockB, TimeSpan.FromDays(1));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        // O ato criador da versão de B existe — mas não neste certame. Sem o filtro
        // de pertença, o snapshot de A viria com o documento de B.
        DadosDocumentaisAto? cruzado = await repository
            .ObterDadosDocumentaisDoAtoAsync(processoA, versaoB.AtoCriadorId, CancellationToken.None);
        cruzado.Should().BeNull("um ato de outro certame não hidrata este snapshot");

        DadosDocumentaisAto? proprio = await repository
            .ObterDadosDocumentaisDoAtoAsync(processoA, versaoA.AtoCriadorId, CancellationToken.None);
        proprio.Should().NotBeNull();
        proprio!.Natureza.Should().Be(nameof(NaturezaEdital.Abertura));
        proprio.DataPublicacao.Should().Be(T0);
        processoB.Should().NotBe(processoA);
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

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da retificação
/// (RN08, ADR-0101/0103/0104): a retificação sucede a versão corrente com um ato que
/// emenda o ato criador dela, e o novo snapshot acrescenta o bloco de retificação
/// preservando integralmente os blocos da abertura; o snapshot da abertura permanece imutável.
/// </summary>
public sealed class RetificacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar[0], 64)).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), null, null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"), 1, [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static DocumentoEdital DocumentoConfirmado(Guid processoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static DadosEdital NovosDados(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), documentoId).Value!;

    /// <summary>
    /// Publica um processo e o retifica em seguida — persistindo as duas versões da
    /// configuração. Usa um relógio manual para dar instantes distintos à abertura e à
    /// retificação, como o certame real faz; a ordem, porém, vem da cadeia de versões,
    /// não desses instantes.
    /// </summary>
    private async Task<(ProcessoSeletivo Processo, VersaoConfiguracao VersaoAbertura, VersaoConfiguracao VersaoRetificacao)>
        PublicarERetificarAsync(string nome)
    {
        RelogioManual clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

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

        clock.Avancar(TimeSpan.FromDays(1));

        // Recarrega o agregado tracked — como o handler faz.
        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);
        VersaoConfiguracao versaoRetificacao;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
                carregado, dadosRetificacao, docRetificacao.HashSha256!,
                // O alvo da retificação é o ato que criou a versão corrente — o
                // topo da cadeia de CONFIGURAÇÃO —, como o handler faz.
                new RetificacaoInfo(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição")));
            Result<VersaoConfiguracao> retificar = carregado.Retificar(
                dadosRetificacao, versaoAtual, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion, canonicoRetificacao.AlgoritmoHash,
                docRetificacao.HashSha256!, "integration-test-user", "Correção do prazo de inscrição", clock);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            versaoRetificacao = retificar.Value!;

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoRetificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo, publicar.Value!, versaoRetificacao);
    }

    [Fact(DisplayName = "A retificação persiste uma segunda versão, criada por um ato que emenda o ato criador da primeira")]
    public async Task Retificacao_PersisteVersaoQueEmendaAAnterior()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao abertura, VersaoConfiguracao retificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_PersisteVersaoQueEmendaAAnterior));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processo.Id)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(CancellationToken.None);

        versoes.Should().HaveCount(2);
        versoes[0].AtoCriadorId.Should().Be(abertura.AtoCriadorId);
        versoes[0].AtoCriadorRetificaId.Should().BeNull("a raiz da cadeia não emenda ninguém");

        versoes[1].AtoCriadorId.Should().Be(retificacao.AtoCriadorId);
        versoes[1].AtoCriadorRetificaId.Should().Be(
            abertura.AtoCriadorId,
            "a linhagem sobrevive à ida e volta do banco: a versão 2 foi criada por um ato que emenda o ato criador da versão 1");

        // O motivo não se perdeu com a tabela de editais: ele está congelado nos bytes
        // canônicos (ADR-0100) e viaja para Publicações na mensagem durável (ADR-0108).
        JsonNode.Parse(versoes[1].ConfiguracaoCongelada)!["retificacao"]!["motivo"]!
            .GetValue<string>().Should().Be("Correção do prazo de inscrição");
    }

    [Fact(DisplayName = "Snapshot de retificação carrega os blocos da abertura mais o bloco retificacao; o snapshot da abertura permanece imutável")]
    public async Task Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel()
    {
        (_, VersaoConfiguracao versaoAbertura, VersaoConfiguracao versaoRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();

        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoAbertura.Id, CancellationToken.None);
        JsonObject payloadAbertura = JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!.AsObject();

        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);
        JsonObject payloadRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!.AsObject();

        // Os blocos esperados na retificação são DERIVADOS do snapshot da abertura, não uma
        // lista literal escrita à mão: são exatamente os blocos da abertura mais o bloco
        // `retificacao` (ADR-0101). Assim, se a abertura ganhar um bloco novo e a retificação
        // não o preservar — qualquer um deles, não só os que alguém lembrou de listar —, esta
        // asserção falha sozinha, sem que ninguém precise atualizar um número.
        IEnumerable<string> blocosEsperados = [.. payloadAbertura.Select(static kvp => kvp.Key), "retificacao"];
        payloadRetificacao.Select(static kvp => kvp.Key).Should().BeEquivalentTo(blocosEsperados,
            "a retificação preserva integralmente os blocos da abertura e acrescenta o bloco retificacao");

        payloadRetificacao["retificacao"]!["motivo"]!.GetValue<string>().Should().Be("Correção do prazo de inscrição");

        // O snapshot da abertura permanece byte-a-byte idêntico (append-only).
        aberturaLida.HashConfiguracao.Should().Be(versaoAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);
        payloadAbertura.Should().NotContainKey("retificacao",
            "o snapshot da abertura nunca carrega o bloco de retificação");
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco (retificação) — re-hashear os bytes lidos do banco bate com o hash persistido")]
    public async Task Retificacao_HashConfereAppEBanco()
    {
        (_, _, VersaoConfiguracao versaoRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_HashConfereAppEBanco));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao lida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);

        HashCanonicalComputer.ComputeSha256Hex(lida.ConfiguracaoCongeladaCanonica)
            .Should().Be(lida.HashConfiguracao);
    }

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }

    // ── Story #575 — a cascata de remanejamento sob a sessão editorial de retificação ──

    /// <summary>
    /// Uma cascata VÁLIDA (cobre as 8 origens SegueCascata de
    /// <c>NovoProcessoComOfertaFederalECascata</c>), mas com forma DIFERENTE da matriz legal
    /// completa (8×7) semeada por ela: um único destino por origem, num mapeamento
    /// LB↔LI diferente. É a "edição durante a sessão" que os dois testes abaixo exercitam —
    /// distinguível da versão original tanto pela contagem de destinos quanto pelo conteúdo.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataDeUmDestinoPorOrigem()
    {
        (string Origem, string Destino)[] pares =
        [
            ("LB_PPI", "LB_Q"), ("LB_Q", "LB_PPI"), ("LB_PCD", "LI_PCD"), ("LB_EP", "LI_EP"),
            ("LI_PPI", "LB_PPI"), ("LI_Q", "LB_Q"), ("LI_PCD", "LB_PCD"), ("LI_EP", "LB_EP"),
        ];
        List<DestinoRemanejamento> destinos = [.. pares.Select(p => DestinoRemanejamento.Criar(p.Origem, 1, p.Destino).Value!)];

        return ConfiguracaoCascataRemanejamento.Criar(
            ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", new string('9', 64)).Value!,
            "AC",
            destinos).Value!;
    }

    [Fact(DisplayName = "Abrir retificação, editar a cascata de remanejamento e fechar gera a versão N+1 com a cascata nova — a versão N permanece intacta")]
    public async Task Retificacao_ComEdicaoDeCascataDuranteASessao_FechamentoGeraVersaoComCascataNova()
    {
        string nome = nameof(Retificacao_ComEdicaoDeCascataDuranteASessao_FechamentoGeraVersaoComCascataNova);
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);

        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        VersaoConfiguracao versaoRetificacao;
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = carregado.AbrirRetificacao(
                "Corrigir a ordem legal de remanejamento", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            // A EDIÇÃO durante a sessão — o que este teste prova: DefinirCascataRemanejamento
            // escreve DIRETO na configuração viva, sem staging, como qualquer outro Definir*.
            carregado.DefinirCascataRemanejamento(CascataDeUmDestinoPorOrigem(), PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

            DocumentoEdital docRetificacao = DocumentoConfirmado(processoId);
            await sessao.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(
                new EntradaCanonicalizacao(carregado, dadosRetificacao, docRetificacao.HashSha256!));

            Result<VersaoConfiguracao> fechar = carregado.FecharRetificacao(
                dadosRetificacao, versaoAbertura, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion,
                canonicoRetificacao.AlgoritmoHash, docRetificacao.HashSha256!, "integration-test-user",
                PrecondicaoIfMatch.Curinga, TimeProvider.System);
            fechar.IsSuccess.Should().BeTrue(fechar.Error?.Message);
            versaoRetificacao = fechar.Value!;

            await repository.AdicionarVersaoConfiguracaoAsync(versaoRetificacao, CancellationToken.None);
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoAbertura.Id, CancellationToken.None);
        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);

        // A versão N permanece byte-a-byte intacta (append-only) — a edição da sessão nunca a alcança.
        aberturaLida.HashConfiguracao.Should().Be(versaoAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);

        retificacaoLida.NumeroVersao.Should().Be(2);
        retificacaoLida.AtoCriadorRetificaId.Should().Be(versaoAbertura.AtoCriadorId);

        JsonObject cascataNaAbertura = JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!["cascataRemanejamento"]!.AsObject();
        JsonObject cascataNaRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!["cascataRemanejamento"]!.AsObject();

        cascataNaAbertura["ordens"]![0]!["destinos"]!.AsArray().Should().HaveCount(7,
            "a versão N congelou a matriz legal completa (8×7) semeada por NovoProcessoComOfertaFederalECascata");
        cascataNaRetificacao["ordens"]![0]!["destinos"]!.AsArray().Should().HaveCount(1,
            "a versão N+1 congela a cascata EDITADA durante a sessão — um destino por origem, não a matriz original");
    }

    [Fact(DisplayName = "Descartar a retificação após editar a cascata restaura exatamente a cascata da versão anterior")]
    public async Task Retificacao_DescartadaAposEditarCascata_RestauraCascataDaVersaoAnterior()
    {
        string nome = nameof(Retificacao_DescartadaAposEditarCascata_RestauraCascataDaVersaoAnterior);
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);

        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = tracked.AbrirRetificacao(
                "Testar edição e descarte da cascata", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            tracked.DefinirCascataRemanejamento(CascataDeUmDestinoPorOrigem(), PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();
            tracked.Cascata!.Destinos.Should().HaveCount(8,
                "pré-condição: a sessão editorial trocou a matriz legal completa (56 destinos) por um destino por origem (8)");

            // O DESCARTE — com a prova de fidelidade, exatamente como em produção
            // (RestauradorDeConfiguracao só repõe DEPOIS de provar byte a byte).
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(tracked, versaoAbertura);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await sessao.SaveChangesAsync(CancellationToken.None);

            tracked.RestaurarConfiguracaoCongelada(versaoAbertura, prova.Value!).IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Status.Should().Be(StatusProcesso.Publicado);
        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.Destinos.Should().HaveCount(56,
            "o descarte restaurou a matriz legal completa que a versão N congelou — não os 8 destinos da edição abandonada");
        relido.Cascata.FallbackCodigo.Should().Be("AC");

        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).ToListAsync(CancellationToken.None);
        versoes.Should().ContainSingle("descartar não cria versão nova — só a abertura persiste");
    }

    [Theory(DisplayName = "Descartar a retificação após editar o formulário de inscrição restaura exatamente o título/termo da versão anterior (Story #559)")]
    [InlineData("Formulário publicado", "Termo publicado", "Formulário editado", "Termo editado")]
    [InlineData(null, null, "Formulário editado", "Termo editado")]
    public async Task Retificacao_DescartadaAposEditarFormulario_RestauraFormularioDaVersaoAnterior(
        string? tituloPublicado, string? termoPublicado, string tituloEditado, string termoEditado)
    {
        string nome = $"{nameof(Retificacao_DescartadaAposEditarFormulario_RestauraFormularioDaVersaoAnterior)}-{Guid.CreateVersion7()}";
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);
        processo.DefinirFormulario(tituloPublicado, termoPublicado, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = tracked.AbrirRetificacao(
                "Testar edição e descarte do formulário", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            tracked.DefinirFormulario(tituloEditado, termoEditado, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            tracked.FormularioTitulo.Should().Be(tituloEditado, "pré-condição: a sessão editorial trocou o título");

            // O DESCARTE — com a prova de fidelidade, exatamente como em produção.
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(tracked, versaoAbertura);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await sessao.SaveChangesAsync(CancellationToken.None);

            tracked.RestaurarConfiguracaoCongelada(versaoAbertura, prova.Value!).IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.FormularioTitulo.Should().Be(
            tituloPublicado, "o descarte restaurou o título que a versão N congelou — não o título editado na sessão abandonada");
        relido.FormularioTermoAceiteTexto.Should().Be(
            termoPublicado, "o descarte restaurou o termo que a versão N congelou — não o termo editado na sessão abandonada");
    }

    // ── issue #563 — a regra de abreviação de nome é DERIVADA no congelamento, não uma
    // configuração viva. A matriz precisa das três direções: publicação, fechamento e
    // descarte não podem ser a mesma prova disfarçada de três formas. ──

    [Fact(DisplayName =
        "issue #563: publicar com nome_abreviado congela a regra vigente; fechar a retificação após removê-lo grava regraNomeAbreviado null na versão nova, sem tocar a versão anterior")]
    public async Task Retificacao_ComRemocaoDeNomeAbreviado_FechamentoGravaRegraNula()
    {
        string nome = nameof(Retificacao_ComRemocaoDeNomeAbreviado_FechamentoGravaRegraNula);
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        JsonObject divulgacaoNaAbertura = JsonNode.Parse(versaoAbertura.ConfiguracaoCongelada)!["divulgacao"]!.AsObject();
        divulgacaoNaAbertura["regraNomeAbreviado"]!.GetValue<string>().Should().Be(
            RegrasDeNomeAbreviado.Vigente, "publicar com nome_abreviado no conjunto congela o identificador da regra vigente");

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        VersaoConfiguracao versaoRetificacao;
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = carregado.AbrirRetificacao(
                "Remover a divulgação do nome abreviado", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            // A EDIÇÃO durante a sessão: remove nome_abreviado — só o piso sobrevive.
            carregado.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();

            DocumentoEdital docRetificacao = DocumentoConfirmado(processoId);
            await sessao.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(
                new EntradaCanonicalizacao(carregado, dadosRetificacao, docRetificacao.HashSha256!));

            Result<VersaoConfiguracao> fechar = carregado.FecharRetificacao(
                dadosRetificacao, versaoAbertura, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion,
                canonicoRetificacao.AlgoritmoHash, docRetificacao.HashSha256!, "integration-test-user",
                PrecondicaoIfMatch.Curinga, TimeProvider.System);
            fechar.IsSuccess.Should().BeTrue(fechar.Error?.Message);
            versaoRetificacao = fechar.Value!;

            await repository.AdicionarVersaoConfiguracaoAsync(versaoRetificacao, CancellationToken.None);
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoAbertura.Id, CancellationToken.None);
        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);

        // A versão N permanece byte-a-byte intacta — a edição da sessão nunca a alcança.
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);

        JsonObject divulgacaoNaRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!["divulgacao"]!.AsObject();
        divulgacaoNaRetificacao["camposPublicos"]!.AsArray().Select(static n => n!.GetValue<string>())
            .Should().Equal("numero_inscricao");
        divulgacaoNaRetificacao["regraNomeAbreviado"].Should().BeNull(
            "o fechamento congela a configuração viva EDITADA (sem nome_abreviado) — a regra derivada tem de zerar " +
            "junto, não carregar o valor da versão anterior");
    }

    [Fact(DisplayName =
        "issue #563: descartar a retificação após remover nome_abreviado restaura o conjunto abreviado e recodifica com o identificador congelado")]
    public async Task Retificacao_DescartadaAposRemoverNomeAbreviado_RestauraConjuntoERegraCongelados()
    {
        string nome = nameof(Retificacao_DescartadaAposRemoverNomeAbreviado_RestauraConjuntoERegraCongelados);
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = tracked.AbrirRetificacao(
                "Testar edição e descarte da divulgação", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            tracked.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();
            tracked.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(
                ["numero_inscricao"], "pré-condição: a sessão editorial removeu nome_abreviado");

            // O DESCARTE — com a prova de fidelidade: RestauradorDeConfiguracao só repõe DEPOIS
            // de recanonicalizar e comparar byte a byte com a versão N. Se a regra derivada não
            // reproduzisse 'iniciais_mais_ultimo_sobrenome' para o conjunto restaurado, a prova
            // falharia aqui — antes de qualquer escrita.
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(tracked, versaoAbertura);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await sessao.SaveChangesAsync(CancellationToken.None);

            tracked.RestaurarConfiguracaoCongelada(versaoAbertura, prova.Value!).IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.ConfiguracaoDivulgacao.Should().NotBeNull();
        relido.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(
            ["nome_abreviado", "numero_inscricao"],
            "o descarte restaurou o conjunto AMPLIADO que a versão N congelou — não o conjunto reduzido editado na sessão abandonada");

        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).ToListAsync(CancellationToken.None);
        versoes.Should().ContainSingle("descartar não cria versão nova — só a abertura persiste");
    }
}

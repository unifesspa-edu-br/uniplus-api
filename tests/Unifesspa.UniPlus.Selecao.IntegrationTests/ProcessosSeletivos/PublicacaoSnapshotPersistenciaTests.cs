namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do
/// congelamento do snapshot de publicação (RN08, ADR-0100, Story #759 T4
/// #785). Mapa de testes de #759: <c>Snapshot_HashConfereAppEBanco</c>
/// (re-hashear os bytes lidos de volta do banco bate com o hash persistido
/// pela app) e <c>Snapshot_Contem24BlocosCanonicos</c> (os 24 blocos — todos
/// reais — estão presentes). Story #575 promoveu <c>cascataRemanejamento</c>
/// de stub a bloco real; issue #849 promoveu <c>identidadesUnidade</c>;
/// Story #559 promoveu <c>formulario</c>; issue #563 promoveu <c>divulgacao</c>
/// de stub a bloco real — a última dimensão provisória da Feature #40, agora
/// fechada: cobertura de persistência EF da entidade filha (sobrevivência a
/// reload, substituição sem órfãos, remoção, constraints do banco) na segunda
/// metade do arquivo.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo com parâmetros ligados — os INSERTs crus de constraint não recebem entrada externa.")]
public sealed class PublicacaoSnapshotPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public PublicacaoSnapshotPersistenciaTests(ProcessoSeletivoDbFixture fixture)
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

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
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
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private async Task<(Guid ProcessoId, Guid EditalId, Guid SnapshotId, ProcessoSeletivo Processo)> PublicarAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue();

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id);
        dadosResult.IsSuccess.Should().BeTrue();

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosResult.Value!, documento.HashSha256!));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(publicarResult.Value!, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        return (processo.Id, publicarResult.Value!.AtoCriadorId, publicarResult.Value!.Id, processo);
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco — re-hashear os bytes lidos do banco bate com o hash persistido pela app")]
    public async Task Snapshot_HashConfereAppEBanco()
    {
        (_, _, Guid snapshotId, _) = await PublicarAsync(nameof(Snapshot_HashConfereAppEBanco));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        string hashRecalculado = HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica);

        hashRecalculado.Should().Be(versao.HashConfiguracao,
            "ADR-0100 §Confirmação: re-hashear os bytes persistidos deve bater com o hash calculado pela aplicação na publicação");
    }

    [Fact(DisplayName = "Snapshot_Contem24BlocosCanonicos — os 24 blocos, todos reais, estão presentes")]
    public async Task Snapshot_Contem24BlocosCanonicos()
    {
        (_, _, Guid snapshotId, _) = await PublicarAsync(nameof(Snapshot_Contem24BlocosCanonicos));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        JsonNode payload = JsonNode.Parse(versao.ConfiguracaoCongelada)!;

        string[] blocosEsperados =
        [
            "tipoProcesso", "periodo", "etapas", "vagas", "distribuicao", "modalidades", "ofertas",
            "atendimento", "bonusRegional", "criteriosDesempate", "classificacao", "hashesEdital",
            "documentosExigidos", "arvoreSatisfacao", "formulario", "cascataRemanejamento", "divulgacao",
            "cronogramaFases", "identidadesUnidade",
            // Story #928, §7.4: coleta de fatos, derivação, grafo conjunto congelado, versão do
            // interpretador e conjunto de modalidades ofertadas.
            "fatosColetados", "regrasDerivacao", "grafoDependencia", "versaoInterpretador", "modalidadesOfertadas",
        ];
        JsonObject objeto = payload.AsObject();
        foreach (string bloco in blocosEsperados)
        {
            objeto.Should().ContainKey(bloco, $"o bloco canônico '{bloco}' deve estar presente no envelope");
        }

        // Equivalência EXATA de conjunto entre as chaves do envelope e a lista literal acima —
        // não só "os blocos esperados estão presentes" (o foreach acima, que por si só não
        // reprovaria uma chave espúria), mas também "não sobra nenhuma outra". A lista literal
        // FICA: este é o teste de contrato da abertura, e derivá-la do próprio objeto o
        // tornaria tautológico — passaria com qualquer conjunto de chaves.
        objeto.Select(static kvp => kvp.Key).Should().BeEquivalentTo(blocosEsperados,
            "o envelope de abertura tem exatamente os blocos canônicos listados acima — nem mais, nem menos");

        string[] stubs = [.. objeto
            .Where(static kvp => kvp.Value is JsonObject bloco
                && bloco.TryGetPropertyValue("status", out JsonNode? status)
                && status?.GetValue<string>() == "nao_construido")
            .Select(static kvp => kvp.Key)
            .Order(StringComparer.Ordinal)];

        stubs.Should().BeEmpty(
            "issue #563 promoveu 'divulgacao' — a última dimensão sem dono da Feature #40 — a bloco real. Os 24 " +
            "blocos são todos reais agora (Story #851 promoveu cronogramaFases; Story #853 promoveu " +
            "documentosExigidos; issue #848 promoveu vagas; Story #575 promoveu cascataRemanejamento; issue #849 " +
            "promoveu identidadesUnidade; Story #559 promoveu formulario; os demais sempre foram reais)");

        // Nenhum bloco REAL emite `nao_construido` na RAIZ. Atendimento, classificação e
        // documentosExigidos são dimensões obrigatórias/já entregues: a ausência da primeira é
        // pendência de conformidade, não stub silencioso; a segunda nunca foi stub.
        // documentosExigidos é totalmente real desde a Story #554 (PR #903, bump 1.2): a sub-chave
        // `exigencias` deixou de ser stub e materializa o item rico de cada DocumentoExigido vivo
        // do processo — vazia aqui porque este fixture não configura nenhuma exigência documental.
        objeto["atendimento"]!.AsObject().Should().NotContainKey("status");
        objeto["classificacao"]!.AsObject().Should().NotContainKey("status");
        objeto["cronogramaFases"]!.AsObject().Should().NotContainKey("status");
        objeto["vagas"]!.AsArray().Should().NotBeEmpty("issue #848: o quadro de vagas é sempre materializado junto da configuração");
        objeto["documentosExigidos"]!.AsObject().Should().NotContainKey("status");
        // issue #849: identidadesUnidade é bloco real desde a criação — nunca stub na raiz.
        objeto["identidadesUnidade"]!.AsObject().Should().NotContainKey("status");
        objeto["identidadesUnidade"]!.AsObject()["administradora"]!.AsObject().Should().ContainKey("sigla");
        // issue #563: divulgacao é bloco real — o default minimizado (só o número de inscrição),
        // porque este teste não configura divulgação nenhuma.
        objeto["divulgacao"]!.AsObject().Should().NotContainKey("status");
        objeto["divulgacao"]!.AsObject()["camposPublicos"]!.AsArray().Should().ContainSingle()
            .Which!.GetValue<string>().Should().Be("numero_inscricao");
        objeto["documentosExigidos"]!["exigencias"]!.AsArray().Should().BeEmpty(
            "nenhum DocumentoExigido foi configurado para este processo neste teste");
        objeto["documentosExigidos"]!["obrigatoriedades"]!.AsArray().Should().BeEmpty(
            "nenhuma ObrigatoriedadeLegal vigente foi cadastrada para este processo neste teste");

        // Blocos reais carregam dado de negócio, não o marcador de stub.
        objeto["etapas"]!.AsArray().Should().NotBeEmpty();
        objeto["bonusRegional"]!["presente"]!.GetValue<bool>().Should().BeFalse();
        objeto["cronogramaFases"]!["fases"]!.AsArray().Should().NotBeEmpty();

        // cascataRemanejamento (Story #575): bloco real, ausência congela `{"presente":false}`,
        // NUNCA o stub `nao_construido` — este fixture não tem nenhuma modalidade SegueCascata
        // (é a mesma AC institucional de bonusRegional acima).
        objeto["cascataRemanejamento"]!.AsObject().Should().NotContainKey("status");
        objeto["cascataRemanejamento"]!["presente"]!.GetValue<bool>().Should().BeFalse();
    }

    // ── Story #554 (PR #903, issue #548) — imunidade pós-publicação: editar a configuração
    // viva durante uma retificação aberta não pode alterar o hash de uma versão já persistida ──

    [Fact(DisplayName = "Editar a configuração viva numa retificação aberta não altera o hash já persistido da versão anterior")]
    public async Task Publicar_RetificacaoAbertaEditaConfiguracaoViva_NaoAlteraHashDaVersaoAnterior()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(Publicar_RetificacaoAbertaEditaConfiguracaoViva_NaoAlteraHashDaVersaoAnterior));
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigenciaOriginal = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigenciaOriginal, 0).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id);
        dadosResult.IsSuccess.Should().BeTrue();

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosResult.Value!, documento.HashSha256!));
        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicarResult.Value!;
        string hashOriginal = versaoAbertura.HashConfiguracao;
        byte[] bytesOriginais = versaoAbertura.ConfiguracaoCongeladaCanonica;

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        // "Editar o cadastro de TipoDocumento" (CA-12) — simulado como uma sessão de
        // retificação que redefine a exigência com um TipoDocumento diferente e persiste a
        // mudança no MESMO DbContext que já gravou a versão anterior.
        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Corrigir código do tipo de documento exigido", versaoAbertura, "user-sub-123", TimeProvider.System.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

        DocumentoExigido exigenciaEditada = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE_EDITADA",
            tipoDocumentoNome: "Documento de identidade (cadastro editado)",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigenciaEditada, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versaoRelida = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .FirstAsync(v => v.Id == versaoAbertura.Id, CancellationToken.None);

        versaoRelida.HashConfiguracao.Should().Be(hashOriginal,
            "editar a configuração viva do processo (aqui, o TipoDocumento de uma exigência, dentro de uma " +
            "sessão de retificação aberta e persistida) não pode alterar o hash de uma versão JÁ persistida");
        versaoRelida.ConfiguracaoCongeladaCanonica.Should().Equal(bytesOriginais,
            "os bytes congelados da versão anterior também permanecem imutáveis — não só o hash resumido");
    }

    // ── Story #575 — persistência EF da cascata de remanejamento ──
    //
    // Os testes abaixo não precisam publicar: DefinirCascataRemanejamento não valida a
    // cobertura por oferta (isso é PendenciaDaCascata, exercitada em
    // ProcessoSeletivoCascataTests, Domain.UnitTests) — só a FORMA (RN-CASCATA-4, na
    // factory). O que está em jogo aqui é o que só o Postgres real prova: o Include de
    // ProcessoSeletivoRepository, o cascade e as constraints da tabela.

    private static ConfiguracaoCascataRemanejamento CascataDeDuasOrigens(string semente) =>
        ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, semente),
            "AC",
            [
                DestinoRemanejamento.Criar("LB_PPI", 1, "LB_Q").Value!,
                DestinoRemanejamento.Criar("LB_PPI", 2, "AC").Value!,
                DestinoRemanejamento.Criar("LB_Q", 1, "AC").Value!,
            ]).Value!;

    [Fact(DisplayName = "Cascata_SobreviveASaveChangesEReload — a cascata e os destinos persistem via Include e sobrevivem a um reload")]
    public async Task Cascata_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(Cascata_SobreviveASaveChangesEReload));
        processo.DefinirCascataRemanejamento(CascataDeDuasOrigens("1"), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        relido.Cascata.Should().NotBeNull(
            "a cascata sobrevive ao SaveChanges e ao reload — sem o Include em ComConfiguracao ela nasceria " +
            "null em todo carregamento novo do agregado");
        relido.Cascata!.ProcessoSeletivoId.Should().Be(processo.Id);
        relido.Cascata.FallbackCodigo.Should().Be("AC");
        relido.Cascata.Destinos.Should().HaveCount(3);
        relido.Cascata.Destinos.Should().OnlyContain(d => d.ConfiguracaoCascataRemanejamentoId == relido.Cascata.Id,
            "as FKs dos destinos apontam para a cascata recém-persistida, não para uma cascata de outro processo");
    }

    [Fact(DisplayName = "Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive — DefinirCascataRemanejamento duas vezes não acumula destinos órfãos")]
    public async Task Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive));
        ConfiguracaoCascataRemanejamento primeiraVersao = CascataDeDuasOrigens("2");
        processo.DefinirCascataRemanejamento(primeiraVersao, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Guid idPrimeiraVersao = primeiraVersao.Id;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Segunda sessão editorial: substitui a cascata inteira por OUTRA — conteúdo
        // diferente, origens diferentes, contagem de destinos diferente.
        ConfiguracaoCascataRemanejamento segundaVersao = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, "3"),
            "AC",
            [DestinoRemanejamento.Criar("LI_PPI", 1, "AC").Value!]).Value!;
        Guid idSegundaVersao = segundaVersao.Id;
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.DefinirCascataRemanejamento(segundaVersao, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.Id.Should().Be(idSegundaVersao, "a cascata VIVA é a SEGUNDA versão — a primeira não sobrevive");
        relido.Cascata.Destinos.Should().ContainSingle(
            "só a SEGUNDA versão da cascata sobrevive — a primeira (3 destinos, origens LB_PPI/LB_Q) " +
            "não pode deixar resíduo")
            .Which.ModalidadeOrigemCodigo.Should().Be("LI_PPI");

        // A prova de "sem órfãos": zero destinos ainda apontando para a PRIMEIRA cascata (que
        // não existe mais) — filtrado pelo id conhecido, não uma contagem cega da tabela
        // inteira, que este teste COMPARTILHA com as demais classes deste fixture
        // (IClassFixture — mesmo Postgres, mesma tabela, entre métodos de teste).
        int destinosOrfaosDaPrimeiraVersao = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idPrimeiraVersao, CancellationToken.None);
        destinosOrfaosDaPrimeiraVersao.Should().Be(0,
            "substituir a cascata inteira via DefinirCascataRemanejamento não pode deixar os 3 destinos da " +
            "primeira versão órfãos em destinos_remanejamento");

        int destinosDaSegundaVersao = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idSegundaVersao, CancellationToken.None);
        destinosDaSegundaVersao.Should().Be(1, "só o destino da segunda versão sobrevive");
    }

    [Fact(DisplayName = "Cascata_Removida_SomeComATabelaDeDestinos — DefinirCascataRemanejamento(null) remove a cascata e os destinos, sem lixo órfão")]
    public async Task Cascata_Removida_SomeComATabelaDeDestinos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nameof(Cascata_Removida_SomeComATabelaDeDestinos));
        ConfiguracaoCascataRemanejamento cascata = CascataDeDuasOrigens("4");
        processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Guid idCascata = cascata.Id;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.Cascata.Should().NotBeNull("pré-condição: a cascata persistiu na sessão anterior");
            tracked.DefinirCascataRemanejamento(null, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.Cascata.Should().BeNull("DefinirCascataRemanejamento(null) remove a cascata — o toggle 'sem cascata' é a própria ausência da entidade");

        // O cascade delete (ON DELETE CASCADE de destinos_remanejamento para
        // configuracoes_cascata_remanejamento) tem de ter levado os destinos junto — a FK
        // cuida disso, mas é exatamente o que se está provando aqui, não presumindo. Filtrado
        // pelo id conhecido: este fixture (IClassFixture) compartilha o mesmo Postgres entre
        // TODOS os métodos de teste da classe, então uma contagem cega da tabela inteira
        // pegaria linhas de outros testes.
        int destinosOrfaos = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idCascata, CancellationToken.None);
        destinosOrfaos.Should().Be(0,
            "remover a cascata tem de levar os destinos junto — nenhuma linha pode sobrar em destinos_remanejamento");

        bool cascataAindaExiste = await readContext.Set<ConfiguracaoCascataRemanejamento>()
            .AnyAsync(c => c.Id == idCascata, CancellationToken.None);
        cascataAindaExiste.Should().BeFalse("a linha da cascata também não pode sobrar em configuracoes_cascata_remanejamento");
    }

    // ── Constraints do banco — o caminho que o agregado NUNCA toma (defesa em profundidade) ──
    //
    // ConfiguracaoCascataRemanejamento.Criar e DestinoRemanejamento.Criar já recusam estas
    // formas (OrdemDuplicadaNaOrigem, DestinoDuplicado, OrdemInvalida, OrigemIgualAoDestino)
    // — os testes abaixo provam que o BANCO recusa sozinho, mesmo se o domínio nunca as
    // produzisse. O INSERT é cru, via NpgsqlCommand, contornando deliberadamente a factory.

    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    [Fact(DisplayName = "Índice único (cascata, origem, ordem) — o banco recusa duas linhas com a mesma posição na fila da mesma origem")]
    public async Task Destinos_MesmaOrigemEOrdem_BancoRejeitaComIndiceUnico()
    {
        Guid cascataId = await SemearCascataVaziaAsync(nameof(Destinos_MesmaOrigemEOrdem_BancoRejeitaComIndiceUnico));

        await InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_Q");

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_PCD"));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_destinos_remanejamento_cascata_origem_ordem");
    }

    [Fact(DisplayName = "Índice único (cascata, origem, destino) — o banco recusa a mesma origem apontando duas vezes para o mesmo destino")]
    public async Task Destinos_MesmaOrigemEDestino_BancoRejeitaComIndiceUnico()
    {
        Guid cascataId = await SemearCascataVaziaAsync(nameof(Destinos_MesmaOrigemEDestino_BancoRejeitaComIndiceUnico));

        await InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_Q");

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 2, "LB_Q"));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_destinos_remanejamento_cascata_origem_destino");
    }

    [Fact(DisplayName = "CHECK ordem >= 1 — o banco recusa uma posição de fila zero ou negativa")]
    public async Task Destinos_OrdemNaoPositiva_BancoRejeitaComCheck()
    {
        Guid cascataId = await SemearCascataVaziaAsync(nameof(Destinos_OrdemNaoPositiva_BancoRejeitaComCheck));

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 0, "LB_Q"));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_destinos_remanejamento_ordem_positiva");
    }

    [Fact(DisplayName = "CHECK origem <> destino — o banco recusa uma modalidade remanejando para si mesma")]
    public async Task Destinos_OrigemIgualAoDestino_BancoRejeitaComCheck()
    {
        Guid cascataId = await SemearCascataVaziaAsync(nameof(Destinos_OrigemIgualAoDestino_BancoRejeitaComCheck));

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_PPI"));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_destinos_remanejamento_origem_diferente_destino");
    }

    /// <summary>
    /// Persiste um processo com uma cascata SEM destinos (o mínimo que
    /// <c>ConfiguracaoCascataRemanejamento.Criar</c> aceitaria é 1 — aqui o destino real vem
    /// depois, cru, pelos testes de constraint) e devolve o id da cascata para os INSERTs
    /// crus. Precisa de ao menos um destino válido para passar pela factory; os testes de
    /// constraint então adicionam um SEGUNDO destino, cru, que colide ou viola.
    /// </summary>
    private async Task<Guid> SemearCascataVaziaAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, "5"),
            "AC",
            [DestinoRemanejamento.Criar("LI_EP", 1, "AC").Value!]).Value!;
        processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        return cascata.Id;
    }

    /// <summary>INSERT cru na tabela de destinos — o caminho que a factory do domínio nunca toma.</summary>
    private async Task InserirDestinoCruAsync(Guid cascataId, string origem, int ordem, string destino)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            """
            INSERT INTO selecao.destinos_remanejamento (
                id, configuracao_cascata_remanejamento_id, modalidade_origem_codigo, ordem, modalidade_destino_codigo,
                created_at)
            VALUES (@id, @cascataId, @origem, @ordem, @destino, now())
            """,
            conexao);
        comando.Parameters.AddWithValue("id", Guid.CreateVersion7());
        comando.Parameters.AddWithValue("cascataId", cascataId);
        comando.Parameters.AddWithValue("origem", origem);
        comando.Parameters.AddWithValue("ordem", ordem);
        comando.Parameters.AddWithValue("destino", destino);

        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<PostgresException> CapturarErroAsync(Func<Task> acao) =>
        (await acao.Should().ThrowAsync<PostgresException>()).Which;
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da persistência EF de
/// <see cref="ConfiguracaoDivulgacao"/> (UNI-REQ-0050, issue #563) — o que só o banco real
/// prova: o <c>Include</c> de <see cref="ProcessoSeletivoRepository"/>, o cascade, a
/// substituição sem resíduo (CA-05) e a estabilidade do <c>ValueComparer</c> do jsonb.
/// </summary>
public sealed class ConfiguracaoDivulgacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public ConfiguracaoDivulgacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

    [Fact(DisplayName = "Divulgacao_SobreviveASaveChangesEReload — a configuração persiste via Include e sobrevive a um reload")]
    public async Task Divulgacao_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SobreviveASaveChangesEReload));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome"], "Transparência do resultado — decisão CEPS 12/2026.").Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().NotBeNull(
            "a configuração sobrevive ao SaveChanges e ao reload — sem o Include em ComConfiguracao ela nasceria " +
            "null em todo carregamento novo do agregado");
        relido.ConfiguracaoDivulgacao!.ProcessoSeletivoId.Should().Be(processo.Id);
        relido.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("nome", "numero_inscricao");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Transparência do resultado — decisão CEPS 12/2026.");
    }

    [Fact(DisplayName = "CA-05: DefinirConfiguracaoDivulgacao duas vezes deixa UMA linha em configuracoes_divulgacao, com os valores da segunda")]
    public async Task Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_SubstituidaDuasVezes_UmaLinhaComOsValoresDaSegunda));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Guid idPrimeiraVersao = processo.ConfiguracaoDivulgacao!.Id;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Segunda "sessão editorial": substitui a configuração inteira por OUTRA — conteúdo
        // diferente (agora com "nome" e justificativa, sem "nome_abreviado").
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome"], "Justificativa da segunda versão.").Value!,
                PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().NotBeNull();
        relido.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("nome", "numero_inscricao");
        relido.ConfiguracaoDivulgacao.Justificativa.Should().Be("Justificativa da segunda versão.");

        int linhas = await readContext.Set<ConfiguracaoDivulgacao>()
            .CountAsync(c => c.ProcessoSeletivoId == processoId, CancellationToken.None);
        linhas.Should().Be(1, "substituir a configuração inteira via DefinirConfiguracaoDivulgacao não pode deixar a primeira versão como resíduo — é UMA linha, com os valores da mutação mais recente");

        bool primeiraVersaoAindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idPrimeiraVersao, CancellationToken.None);
        primeiraVersaoAindaExiste.Should().BeFalse("a primeira versão não pode sobreviver à substituição");
    }

    [Fact(DisplayName = "Divulgacao_Removida_SomeDaTabela — DefinirConfiguracaoDivulgacao(null) remove a linha, sem resíduo")]
    public async Task Divulgacao_Removida_SomeDaTabela()
    {
        ProcessoSeletivo processo = NovoProcesso(nameof(Divulgacao_Removida_SomeDaTabela));
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Guid idConfiguracao = processo.ConfiguracaoDivulgacao!.Id;

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
            tracked.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a configuração persistiu na sessão anterior");
            tracked.DefinirConfiguracaoDivulgacao(null, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.ConfiguracaoDivulgacao.Should().BeNull(
            "DefinirConfiguracaoDivulgacao(null) remove a configuração — o toggle 'sem configuração explícita' é a própria ausência da entidade");

        bool aindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idConfiguracao, CancellationToken.None);
        aindaExiste.Should().BeFalse("a linha não pode sobrar em configuracoes_divulgacao");
    }

    [Fact(DisplayName = "Duas requisições equivalentes em ordem diferente persistem os campos públicos na MESMA ordem canônica")]
    public async Task Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem()
    {
        // DefinirConfiguracaoDivulgacao substitui a entidade inteira a cada chamada (mesmo molde
        // de DefinirBonusRegional/DefinirCascataRemanejamento: Id novo por Criar, nunca mutação
        // em local da mesma linha) — então duas chamadas nunca geram um UPDATE na MESMA linha, e
        // sim delete-e-insere. O que a ordem canônica garante aqui não é evitar um UPDATE
        // espúrio (não há UPDATE possível neste desenho), e sim que os BYTES persistidos nunca
        // divirjam do que o envelope emitiria para o mesmo conjunto — não importa em que ordem o
        // cliente o enviou.
        ProcessoSeletivo processoA = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-A");
        processoA.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["nome_abreviado", "numero_inscricao"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ProcessoSeletivo processoB = NovoProcesso($"{nameof(Divulgacao_RequisicoesEquivalentes_PersistemNaMesmaOrdem)}-B");
        processoB.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processoA, CancellationToken.None);
            await repository.AdicionarAsync(processoB, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relidoA = (await leitura.ObterComConfiguracaoAsync(processoA.Id, CancellationToken.None))!;
        ProcessoSeletivo relidoB = (await leitura.ObterComConfiguracaoAsync(processoB.Id, CancellationToken.None))!;

        relidoA.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(relidoB.ConfiguracaoDivulgacao!.CamposPublicos);
        relidoA.ConfiguracaoDivulgacao.CamposPublicos.Should().Equal("nome_abreviado", "numero_inscricao");
    }

    // ── D5 — o colapso "linha explícita igual ao default" → ausência precisa de teste de
    // persistência próprio: sem ele, o efeito destrutivo do Cascade fica documentado no plano
    // e não verificado no código. ──

    private static readonly string HashFixo = new('a', 64);
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = NovoProcesso(nome);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            null, null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!, null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!, 1, [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: false, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName =
        "D5: uma linha explícita igual ao default some da tabela quando o descarte restaura o envelope congelado (que nunca teve divulgação configurada) — e o processo continua emitindo o MESMO bloco default")]
    public async Task Divulgacao_ColapsoDeD5_LinhaExplicitaIgualAoDefaultSomeAoDescartar()
    {
        string nome = nameof(Divulgacao_ColapsoDeD5_LinhaExplicitaIgualAoDefaultSomeAoDescartar);
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        // O processo NUNCA configurou divulgação antes de publicar — o bloco congelado é o
        // default minimizado (D5), como se ConfiguracaoDivulgacao fosse null.

        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, HashFixo));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, HashFixo, "integration-test-user", TimeProvider.System);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        VersaoConfiguracao versaoAbertura = publicar.Value!;
        byte[] bytesCongelados = versaoAbertura.ConfiguracaoCongeladaCanonica;

        Guid processoId = processo.Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoAbertura, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        Guid idDaLinhaExplicita;
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(sessao, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            Result<RascunhoRetificacao> abertura = tracked.AbrirRetificacao(
                "Gravar uma linha explícita igual ao default", versaoAbertura, "integration-test-user", TimeProvider.System.GetUtcNow());
            abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

            // A linha VIVA agora é EXPLÍCITA, mas com o MESMO conteúdo do default — o cenário
            // que D5 trata: o processo continua publicamente equivalente, mas passou a ter uma
            // linha na tabela (Id, CreatedAt) que a versão congelada nunca teve.
            tracked.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
            idDaLinhaExplicita = tracked.ConfiguracaoDivulgacao!.Id;
        }

        await using (SelecaoDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(descarte, TimeProvider.System);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;
            tracked.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a linha explícita persistiu na sessão anterior");

            // O DESCARTE — a prova de fidelidade decodifica o envelope congelado (o default
            // minimizado) e, por D5, o grafo restaurado traz ConfiguracaoDivulgacao null.
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(tracked, versaoAbertura);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);
            prova.Value!.ConfiguracaoDivulgacao.Should().BeNull(
                "D5: o bloco congelado é o default minimizado — a decodificação não fabrica entidade");

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync(CancellationToken.None);

            tracked.RestaurarConfiguracaoCongelada(versaoAbertura, prova.Value!).IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();

        // (a) A linha explícita não sobrevive — o cascade a levou junto quando o grafo
        // restaurado reatribuiu ConfiguracaoDivulgacao para null.
        bool linhaAindaExiste = await readContext.Set<ConfiguracaoDivulgacao>()
            .AnyAsync(c => c.Id == idDaLinhaExplicita, CancellationToken.None);
        linhaAindaExiste.Should().BeFalse(
            "D5: a presença da linha não é informação de domínio nem de auditoria — a restauração canonicaliza a " +
            "equivalência (linha explícita igual ao default) para ausência, e o Cascade a remove");

        ProcessoSeletivoRepository leitura = new(readContext, TimeProvider.System);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        // (b) O processo continua servindo o MESMO efetivo público — ausência, não uma entidade.
        relido.ConfiguracaoDivulgacao.Should().BeNull();

        // (c) E os bytes recanonicalizados a partir do estado relido do banco batem com os
        // originalmente congelados — a prova de que "ausência" e "linha explícita igual ao
        // default" produzem exatamente o mesmo documento público. MESMOS DadosEdital da
        // publicação original — um DocumentoEditalId diferente mudaria 'hashesEdital' e a
        // comparação de bytes provaria outra coisa.
        Result<SnapshotCanonico> recodificado = new RegistroCodecsEnvelope().Recodificar(
            versaoAbertura.SchemaVersion, new EntradaCanonicalizacao(relido, dados, HashFixo));
        recodificado.IsSuccess.Should().BeTrue(recodificado.Error?.Message);
        recodificado.Value!.Bytes.Should().Equal(bytesCongelados,
            "o processo relido do banco, sem a linha explícita, recanonicaliza nos MESMOS bytes que o ato publicado congelou");
    }
}

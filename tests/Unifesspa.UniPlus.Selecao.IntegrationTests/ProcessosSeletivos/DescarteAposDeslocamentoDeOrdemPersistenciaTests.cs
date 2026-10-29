namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O descarte de uma sessão editorial que <b>deslocou as ordens do cronograma</b>, contra
/// Postgres real.
/// </summary>
/// <remarks>
/// <para>
/// É o cruzamento de duas reconciliações que não conversam: <c>ProcessoSeletivo.AplicarGrafo</c>
/// casa fase por <c>Ordem</c>, e <c>FaseCronograma.AtualizarSnapshot</c> casa produto por
/// <c>AtoCodigo</c> dentro da fase. Inserir uma fase no meio do cronograma faz a fase congelada
/// de uma ordem cair sobre uma fase viva diferente, e o produto congelado — que carrega
/// <c>Id</c> — passa a atravessar a fronteira da fase que o congelou.
/// </para>
/// <para>
/// O teste precisa de banco: o estrago é do identity map do EF e das linhas de
/// <c>produtos_da_fase</c>, e nenhuma das duas coisas existe num agregado em memória. A
/// mutação intermediária tem de ser <c>DefinirCronogramaFases</c> — e não outra reposição —
/// porque é ela que preserva o <c>Id</c> dos produtos ao renumerar as fases, que é a
/// pré-condição do defeito.
/// </para>
/// </remarks>
public sealed class DescarteAposDeslocamentoDeOrdemPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("de01234567", 7))[..64];
    private static readonly DateTimeOffset Agora = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public DescarteAposDeslocamentoDeOrdemPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Descartar a sessão que inseriu uma fase no meio repõe os produtos nas fases que os congelaram, sem duplicar identidade")]
    public async Task Descarte_AposInserirFaseNoMeio_ReporProdutosSemColidirNoIdentityMap()
    {
        Guid processoId;
        Guid versaoId;
        Guid produtoPreliminarId;
        Guid produtoFinalId;
        VersaoConfiguracao versao;
        Guid faseInscricaoOrigem = Guid.CreateVersion7();
        Guid famePreliminarOrigem = Guid.CreateVersion7();
        Guid faseFinalOrigem = Guid.CreateVersion7();

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS deslocamento {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "RESULTADO_PRELIMINAR", famePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(3, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            Result<VersaoConfiguracao> publicacao = Publicar(processo);
            publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
            processo.ClearDomainEvents();
            versao = publicacao.Value!;

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.AddAsync(versao);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            versaoId = versao.Id;
            produtoPreliminarId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id;
            produtoFinalId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo).Id;
        }

        // A sessão editorial insere HOMOLOGACAO na ordem 2 e empurra as duas fases de
        // resultado para 3 e 4. Nenhuma ordem forma ciclo fechado, então
        // PermutacaoDeOrdemNaoSuportada não recusa; as fases são reconciliadas por
        // FaseCanonicaOrigemId e os produtos mantêm seus Ids no banco, agora sob outras ordens.
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.AbrirRetificacao("Insere a homologação no meio do cronograma", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(tracked), "teste", Agora)
                .IsSuccess.Should().BeTrue();

            Result deslocamento = tracked.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "HOMOLOGACAO", Guid.CreateVersion7(),
                        [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
                    Fase(3, "RESULTADO_PRELIMINAR", famePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(4, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Curinga);

            deslocamento.IsSuccess.Should().BeTrue(deslocamento.Error?.Message);
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext conferencia = _fixture.CreateDbContext())
        {
            ProcessoSeletivo deslocado = await CarregarAsync(conferencia, processoId);
            deslocado.CronogramaFases.Should().HaveCount(4, "pré-condição: a sessão inseriu a fase no meio");
            ProdutoDe(deslocado, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id.Should().Be(produtoPreliminarId,
                "pré-condição: renumerar a fase preserva o Id do produto — é o que faz o Id congelado colidir no descarte");
        }

        // O DESCARTE: repõe o grafo congelado — três fases, com a de ordem 2 caindo sobre a
        // HOMOLOGACAO viva — e encerra a sessão.
        await using (SelecaoDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);
            VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);

            Result reposicao = tracked.RestaurarConfiguracaoCongelada(
                versaoDoBanco,
                GrafoCongelado(faseInscricaoOrigem, famePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId));
            reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);

            Result descarteResult = tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga);
            descarteResult.IsSuccess.Should().BeTrue(descarteResult.Error?.Message);

            Func<Task> salvar = async () => await descarte.SaveChangesAsync();

            await salvar.Should().NotThrowAsync(
                "deixar o Id congelado de um produto entrar numa fase que não é a que o congelou põe a MESMA " +
                "linha em duas fases dentro da mesma transação — a rastreada saindo de uma como órfã e a " +
                "congelada entrando na outra —, e o EF derruba o descarte inteiro, deixando o operador sem " +
                "como desfazer o deslocamento");
        }

        await using (SelecaoDbContext verificacao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);

            reposto.CronogramaFases.Should().HaveCount(3, "o descarte repõe o cronograma congelado");

            FaseCronograma preliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
            FaseCronograma final = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_FINAL");

            preliminar.Produtos.Should().ContainSingle()
                .Which.AtoCodigo.Should().Be("RESULTADO_CLASSIFICACAO");
            final.Produtos.Should().ContainSingle()
                .Which.AtoCodigo.Should().Be("RESULTADO_CLASSIFICACAO");

            reposto.CronogramaFases.SelectMany(f => f.Produtos).Select(p => p.Id)
                .Should().OnlyHaveUniqueItems("nenhum produto pode existir em duas fases ao mesmo tempo");

            foreach (FaseCronograma fase in reposto.CronogramaFases)
            {
                fase.Produtos.Should().OnlyContain(p => p.FaseCronogramaId == fase.Id,
                    "o produto pertence à fase que o declara");
            }
        }
    }

    [Fact(DisplayName = "Descartar a sessão que inseriu uma fase no meio deixa a âncora do recurso resolvendo para o produto preliminar reposto")]
    public async Task Descarte_AposInserirFaseNoMeio_AncoraDoRecursoSegueOProdutoPreliminar()
    {
        Guid processoId;
        Guid versaoId;
        Guid produtoPreliminarId;
        Guid produtoFinalId;
        Guid faseInscricaoOrigem = Guid.CreateVersion7();
        Guid fasePreliminarOrigem = Guid.CreateVersion7();
        Guid faseFinalOrigem = Guid.CreateVersion7();

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS âncora deslocada {Guid.CreateVersion7()}");

            // O prazo de interposição corre em dia útil, e a publicação só passa com a
            // convenção de contagem declarada e o calendário vigente em mãos.
            processo.DefinirAlgoritmoContagemPrazo(
                Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, 'f'), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();

            ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
            processo.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem, [preliminar],
                        faseConcluinteCodigo: "RESULTADO_FINAL",
                        regraRecurso: Recurso(preliminar.Id)),
                    Fase(3, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            Result<VersaoConfiguracao> publicacao = Publicar(processo, ComCalendario());
            publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
            processo.ClearDomainEvents();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.AddAsync(publicacao.Value!);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            versaoId = publicacao.Value!.Id;
            produtoPreliminarId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id;
            produtoFinalId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo).Id;
        }

        // A sessão editorial insere HOMOLOGACAO na ordem 2 e empurra as fases de resultado
        // para 3 e 4. A reconciliação por FaseCanonicaOrigemId reusa a instância RASTREADA de
        // cada produto — o Id que chega na coleção nova é descartado, e a âncora declarada
        // sobre ele tem de acompanhar.
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            VersaoConfiguracao versaoDoBanco = await sessao.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);
            tracked.AbrirRetificacao("Insere a homologação no meio do cronograma", versaoDoBanco, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(tracked), "teste", Agora)
                .IsSuccess.Should().BeTrue();

            ProdutoDaFase preliminarDaSessao = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
            preliminarDaSessao.Id.Should().NotBe(produtoPreliminarId,
                "pré-condição: a coleção que chega traz identidade nova, e é a rastreada que sobrevive");

            Result deslocamento = tracked.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "HOMOLOGACAO", Guid.CreateVersion7(),
                        [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
                    Fase(3, "RESULTADO_PRELIMINAR", fasePreliminarOrigem, [preliminarDaSessao],
                        faseConcluinteCodigo: "RESULTADO_FINAL",
                        regraRecurso: Recurso(preliminarDaSessao.Id)),
                    Fase(4, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Curinga);

            deslocamento.IsSuccess.Should().BeTrue(deslocamento.Error?.Message);
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext conferencia = _fixture.CreateDbContext())
        {
            ProcessoSeletivo deslocado = await CarregarAsync(conferencia, processoId);
            AncoraDaFase(deslocado, "RESULTADO_PRELIMINAR").Should().Be(produtoPreliminarId,
                "a redefinição do cronograma reusa o produto rastreado, e a âncora tem de apontar para ele — " +
                "não para a instância que a coleção nova trouxe e o SaveChanges descartou");
        }

        // O DESCARTE: repõe o grafo congelado — três fases, com a de ordem 2 caindo sobre a
        // HOMOLOGACAO viva, o que faz os produtos serem RECRIADOS com identidade nova.
        await using (SelecaoDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);
            VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);

            Result reposicao = tracked.RestaurarConfiguracaoCongelada(
                versaoDoBanco,
                GrafoCongeladoComRecurso(
                    faseInscricaoOrigem, fasePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId));
            reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);

            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();

            await descarte.SaveChangesAsync();
        }

        await using (SelecaoDbContext verificacao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);
            FaseCronograma preliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");

            preliminar.RegraRecurso.Should().NotBeNull("o descarte repõe a regra de recurso congelada");
            preliminar.RegraRecurso!.ProdutoAncoraId.Should().Be(
                preliminar.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id,
                "a âncora resolve para o produto preliminar DESTA fase depois da restauração — a identidade " +
                "congelada foi descartada junto com o produto que a carregava, e uma âncora deixada para trás " +
                "só apareceria quando um ato publicado fosse procurar o prazo que lhe corresponde");

            reposto.CronogramaFases
                .Where(f => f.RegraRecurso is not null)
                .Should().OnlyContain(
                    f => f.Produtos.Any(p => p.Id == f.RegraRecurso!.ProdutoAncoraId),
                    "nenhuma regra de recurso pode ancorar fora dos produtos da própria fase");
        }
    }

    [Fact(DisplayName = "Depois do descarte que recria os produtos, o item já publicado continua barrado para uma segunda publicação original")]
    public async Task Descarte_RecriaOsProdutos_ItemJaPublicadoContinuaBarrado()
    {
        Guid processoId;
        Guid versaoId;
        Guid produtoPreliminarId;
        Guid produtoFinalId;
        Guid documentoId;
        VersaoConfiguracao versao;
        Guid faseInscricaoOrigem = Guid.CreateVersion7();
        Guid fasePreliminarOrigem = Guid.CreateVersion7();
        Guid faseFinalOrigem = Guid.CreateVersion7();

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS item publicado {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(3, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            Result<VersaoConfiguracao> publicacao = Publicar(processo);
            publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
            processo.ClearDomainEvents();
            versao = publicacao.Value!;

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.AddAsync(versao);
            DocumentoAPublicar documento = Documento(processo.Id);
            await db.AddAsync(documento);
            await db.SaveChangesAsync();

            ProdutoDaFase preliminar = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
            AtoDoProcesso publicado = AtoDoItem(
                processo.Id, documento.Id, IdentidadeDoItemDoEdital.DaFase(
                    processo.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR"), preliminar));
            await db.AddAsync(publicado);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            versaoId = versao.Id;
            produtoPreliminarId = preliminar.Id;
            produtoFinalId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo).Id;
            documentoId = documento.Id;
        }

        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.AbrirRetificacao("Insere a homologação no meio do cronograma", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(tracked), "teste", Agora)
                .IsSuccess.Should().BeTrue();
            tracked.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "HOMOLOGACAO", Guid.CreateVersion7(),
                        [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
                    Fase(3, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(4, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);
            VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);
            tracked.RestaurarConfiguracaoCongelada(
                versaoDoBanco,
                GrafoCongelado(faseInscricaoOrigem, fasePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId))
                .IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        await using SelecaoDbContext verificacao = _fixture.CreateDbContext();
        ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);
        FaseCronograma fasePreliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
        ProdutoDaFase recriado = fasePreliminar.Produtos.Single();
        recriado.Id.Should().NotBe(produtoPreliminarId, "pré-condição: o descarte sobre a fase deslocada recria o produto com identidade nova");

        AtoDoProcesso segunda = AtoDoItem(processoId, documentoId, IdentidadeDoItemDoEdital.DaFase(fasePreliminar, recriado));
        await verificacao.AddAsync(segunda);
        Func<Task> salvar = async () => await verificacao.SaveChangesAsync();

        (await salvar.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ux_atos_do_processo_item_original");
    }

    /// <summary>
    /// O grafo congelado do cenário da âncora: igual ao de <see cref="GrafoCongelado"/>, com a
    /// fase preliminar declarando a regra de recurso ancorada no produto congelado.
    /// </summary>
    private static GrafoConfiguracao GrafoCongeladoComRecurso(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) => new(
        etapas: [],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: Classificacao(),
        cronogramaFases:
        [
            FaseReidratada(1, "INSCRICAO", faseInscricaoOrigem, [], null),
            FaseReidratada(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                [ProdutoDaFase.Reidratar(produtoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                "RESULTADO_FINAL",
                Recurso(produtoPreliminarId)),
            FaseReidratada(3, "RESULTADO_FINAL", faseFinalOrigem,
                [ProdutoDaFase.Reidratar(produtoFinalId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                null),
        ],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        configuracaoTaxaInscricao: ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
        localidade: LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        numeroDeOpcoesDeCurso: 1);

    private static Guid AncoraDaFase(ProcessoSeletivo processo, string codigo) =>
        processo.CronogramaFases.Single(f => f.Codigo == codigo).RegraRecurso!.ProdutoAncoraId;

    /// <summary>
    /// O grafo tal como o codec o entrega ao descarte: fases e produtos reidratados com os
    /// <c>Id</c>s congelados no envelope.
    /// </summary>
    private static GrafoConfiguracao GrafoCongelado(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) => new(
        etapas: [],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: Classificacao(),
        cronogramaFases:
        [
            FaseReidratada(1, "INSCRICAO", faseInscricaoOrigem, [], null),
            FaseReidratada(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                [ProdutoDaFase.Reidratar(produtoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                "RESULTADO_FINAL"),
            FaseReidratada(3, "RESULTADO_FINAL", faseFinalOrigem,
                [ProdutoDaFase.Reidratar(produtoFinalId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                null),
        ],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        configuracaoTaxaInscricao: ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
        localidade: LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        numeroDeOpcoesDeCurso: 1);

    private static ProcessoSeletivo NovoProcesso(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([Distribuicao()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(Classificacao(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    private static ConfiguracaoDistribuicaoVagas Distribuicao() => ConfiguracaoDistribuicaoVagas.Criar(
        Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
        null, null,
        [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
                null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
                quantidadeDeclarada: 40).Value!,
        ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static ConfiguracaoClassificacao Classificacao() => ConfiguracaoClassificacao.Criar(
        Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
        Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    /// <summary>
    /// Calendário vigente mínimo — um feriado nacional basta. Só o cenário do prazo de recurso
    /// precisa dele: sem calendário, a publicação recusaria por uma pendência que não é a
    /// testada ali.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo,
        ContextoDeContagemDePrazos? contexto = null) => processo.Publicar(
        DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        "{}"u8.ToArray(),
        "1.1",
        "canonical-json/sha256@v1",
        HashFixo,
        "teste",
        TimeProvider.System,
        contexto ?? ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static DocumentoAPublicar Documento(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static AtoDoProcesso AtoDoItem(Guid processoId, Guid documentoId, IdentidadeDoItemDoEdital item) =>
        AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, "RESULTADO_CLASSIFICACAO", "Resultado da classificação", "1/2026", new DateOnly(2026, 1, 20),
                "Diretor do CEPS", "ceps", "ps-item-publicado", documentoId, [], ItemDoEdital: item),
            TimeProvider.System).Value!;

    private static ProdutoDaFase ProdutoDe(ProcessoSeletivo processo, string atoCodigo, PapelProdutoFase papel) =>
        processo.CronogramaFases.SelectMany(f => f.Produtos).Single(p => p.AtoCodigo == atoCodigo && p.Papel == papel);

    private static FaseCronograma Fase(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo = null,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Criar(
            ordem, faseCanonicaOrigemId, codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos, faseConcluinteCodigo, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso, UnidadesDeTeste.Ceps).Value!;

    private static FaseCronograma FaseReidratada(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(), ordem, faseCanonicaOrigemId, codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos, faseConcluinteCodigo, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso, UnidadesDeTeste.Ceps);

    private static RegraRecursoFase Recurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'e'),
        new ArgsRegraPrazoRecurso(
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null),
        produtoAncoraId).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.OfertaAtendimento)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Rascunho)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}

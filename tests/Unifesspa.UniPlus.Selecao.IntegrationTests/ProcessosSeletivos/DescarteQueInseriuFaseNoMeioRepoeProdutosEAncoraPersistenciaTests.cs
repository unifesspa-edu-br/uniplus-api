namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoDescarteAposDeslocamentoDeOrdem;

/// <remarks>Contra Postgres real: o estrago do descarte é do identity map do EF e das linhas de produtos da fase.</remarks>
public sealed class DescarteQueInseriuFaseNoMeioRepoeProdutosEAncoraPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public DescarteQueInseriuFaseNoMeioRepoeProdutosEAncoraPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
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

        await using (UniPlusDbContext db = _fixture.CreateDbContext())
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
        await using (UniPlusDbContext sessao = _fixture.CreateDbContext())
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

        await using (UniPlusDbContext conferencia = _fixture.CreateDbContext())
        {
            ProcessoSeletivo deslocado = await CarregarAsync(conferencia, processoId);
            deslocado.CronogramaFases.Should().HaveCount(4, "pré-condição: a sessão inseriu a fase no meio");
            ProdutoDe(deslocado, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar).Id.Should().Be(produtoPreliminarId,
                "pré-condição: renumerar a fase preserva o Id do produto — é o que faz o Id congelado colidir no descarte");
        }

        // O DESCARTE: repõe o grafo congelado — três fases, com a de ordem 2 caindo sobre a
        // HOMOLOGACAO viva — e encerra a sessão.
        await using (UniPlusDbContext descarte = _fixture.CreateDbContext())
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

        await using (UniPlusDbContext verificacao = _fixture.CreateDbContext())
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

        await using (UniPlusDbContext db = _fixture.CreateDbContext())
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
        await using (UniPlusDbContext sessao = _fixture.CreateDbContext())
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

        await using (UniPlusDbContext conferencia = _fixture.CreateDbContext())
        {
            ProcessoSeletivo deslocado = await CarregarAsync(conferencia, processoId);
            AncoraDaFase(deslocado, "RESULTADO_PRELIMINAR").Should().Be(produtoPreliminarId,
                "a redefinição do cronograma reusa o produto rastreado, e a âncora tem de apontar para ele — " +
                "não para a instância que a coleção nova trouxe e o SaveChanges descartou");
        }

        // O DESCARTE: repõe o grafo congelado — três fases, com a de ordem 2 caindo sobre a
        // HOMOLOGACAO viva, o que faz os produtos serem RECRIADOS com identidade nova.
        await using (UniPlusDbContext descarte = _fixture.CreateDbContext())
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

        await using (UniPlusDbContext verificacao = _fixture.CreateDbContext())
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
        atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
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
}

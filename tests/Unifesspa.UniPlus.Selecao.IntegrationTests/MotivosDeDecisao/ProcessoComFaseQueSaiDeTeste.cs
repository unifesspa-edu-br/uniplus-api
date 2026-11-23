namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using AwesomeAssertions;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo publicável com duas fases: a da inscrição, que tem o formulário e fica, e a de avaliação, que pode sair
/// do cronograma e leva consigo a etapa "Prova Objetiva".
/// </summary>
internal static class ProcessoComFaseQueSaiDeTeste
{
    internal const string CodigoDaFaseQueSai = "AVALIACAO";

    internal const string CodigoDaFaseQueFica = "INSCRICAO";

    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(IServiceProvider services, string nome)
    {
        Guid inscricao = await LugaresDoProcessoDeTeste.FaseDoCatalogoAsync(services, CodigoDaFaseQueFica);
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) =
            await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = nome, Complementar = AcrescentarAFaseQueSai });
        await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.fases_cronograma SET tipo_fase_origem_id = {inscricao}, codigo = {CodigoDaFaseQueFica}, agrupa_etapas = FALSE WHERE processo_seletivo_id = {processo.Id} AND codigo <> {CodigoDaFaseQueSai}");

        return (processo.Id, documento.Id);
    }

    private static void AcrescentarAFaseQueSai(ProcessoSeletivo processo)
    {
        FaseCronograma faseDaInscricao = processo.CronogramaFases.Single();
        Result cronograma = processo.DefinirCronogramaFases([faseDaInscricao, FaseDeAvaliacao()], []);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        Result etapas = processo.DefinirEtapas([ProvaObjetivaNaFaseDeAvaliacao()]);
        etapas.IsSuccess.Should().BeTrue(etapas.Error?.Message);
    }

    private static FaseCronograma FaseDeAvaliacao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: CodigoDaFaseQueSai, OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso ProvaObjetivaNaFaseDeAvaliacao() => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(
            Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, Ordem: 1, FaseCodigo: CodigoDaFaseQueSai,
            TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!),
        new DeclaracaoDaAvaliacaoDaEtapa(
            Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null,
            EscalaDeAtendimento: null)).Value!;
}

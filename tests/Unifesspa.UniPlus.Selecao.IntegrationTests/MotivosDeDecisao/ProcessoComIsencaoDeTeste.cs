namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo publicável com um fundamento de isenção habilitado: a taxa é cobrada com o fundamento e a janela da isenção
/// é uma fase própria, que declara o tipo de decisão da isenção. Para publicar, essa fase precisa de ao menos um motivo
/// favorável e um desfavorável escolhidos para o fundamento.
/// </summary>
internal static class ProcessoComIsencaoDeTeste
{
    internal const string Fundamento = "CADASTRO_UNICO";
    internal const string CodigoDaFase = "ISENCAO_DA_TAXA";

    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(IServiceProvider services, string nome, string[]? tiposDeDecisao = null)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}", Complementar = processo => Habilitar(processo, tiposDeDecisao ?? [TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo()]) });

        return (processo.Id, documento.Id);
    }

    /// <summary>
    /// Abre a janela da isenção em fase própria, que fecha antes das inscrições, com os tipos de decisão que ela declara, e
    /// cobra a taxa com um fundamento.
    /// </summary>
    internal static void Habilitar(ProcessoSeletivo processo, string[] tiposDeDecisao)
    {
        FaseCronograma isencao = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: CodigoDaFase, OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: true),
            new DecisoesDeCronogramaDaFase(
                Ordem: 2, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(
                Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: tiposDeDecisao),
            bancasRequeridas: []).Value!;
        processo.DefinirCronogramaFases([processo.CronogramaFases.Single(), isencao], [])
            .IsSuccess.Should().BeTrue();
        ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(cobra: true, valor: 100m, fundamentosCodigos: [Fundamento]).Value!;
        processo.DefinirTaxaInscricao(taxa).IsSuccess.Should().BeTrue();
    }
}

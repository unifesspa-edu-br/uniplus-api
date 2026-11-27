namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A isenção da taxa nos processos de teste: a fase que coleta o pedido e o formulário facultativo dele, que o processo
/// que cobra taxa precisa ter para ser publicado.
/// </summary>
internal static class IsencaoDaTaxaDeTeste
{
    private static readonly DateTimeOffset InicioQuandoOProcessoNaoTemInscricao = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static FaseCronograma FaseDeIsencaoDaTaxa(int ordem, DateTimeOffset inicio, DateTimeOffset fim) =>
        FaseCronograma.Criar(
            ChaveLocal.Criar("fase_isencao").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                origemId: Guid.CreateVersion7(), codigo: "ISENCAO_DA_TAXA", origemData: OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false,
                coletaSolicitacaoIsencao: true),
            new DecisoesDeCronogramaDaFase(Ordem: ordem, Inicio: inicio, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: [TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo()]),
            bancasRequeridas: []).Value!;

    /// <summary>
    /// Dá ao processo que cobra taxa o formulário facultativo do pedido de isenção, na fase de solicitação de isenção:
    /// a fase entra no cronograma quando o processo não a tem. O processo que não cobra taxa, ou que já tem o
    /// formulário, fica como está.
    /// </summary>
    internal static void GarantirOFormularioDeQuemCobraTaxa(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        if (processo.ConfiguracaoTaxaInscricao is not { Cobra: true } || TemFormularioFacultativoNaIsencao(processo))
        {
            return;
        }

        DefinirFormularioEFatosDaIsencao(processo, FaseDaIsencao(processo) ?? IncluirFaseDeIsencao(processo));
    }

    internal static void DefinirFormularioEFatosDaIsencao(ProcessoSeletivo processo, Guid faseId)
    {
        processo.DefinirFormularioSemConjuntoBasico(FormulariosDoProcessoDeTeste.PedidoDeIsencao, faseId, obrigatorio: false);
        Result fatos = processo.DefinirFatosColetados(
            FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.PedidoDeIsencao),
            [FatoColetadoDeTeste.Coletando(IsencaoPorCarenciaSocioeconomica.FatoRenda, 0)
                .ComRotulo("Renda familiar per capita de até um salário mínimo e meio")
                .DoTipo(TipoDeCampo.SimOuNao)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .NaSecao(FormularioDeTeste.Secao)
                .Construir()]);
        if (fatos.IsFailure)
        {
            throw new InvalidOperationException($"Os fatos do pedido de isenção de teste não puderam ser definidos: {fatos.Error?.Message}");
        }
    }

    private static bool TemFormularioFacultativoNaIsencao(ProcessoSeletivo processo)
    {
        HashSet<Guid> fasesDeIsencao = [.. processo.CronogramaFases.Where(static f => f.ColetaSolicitacaoIsencao).Select(static f => f.Id)];
        return processo.Formularios.Any(f => !f.Obrigatorio && f.FaseId is { } faseId && fasesDeIsencao.Contains(faseId));
    }

    private static Guid? FaseDaIsencao(ProcessoSeletivo processo) =>
        processo.CronogramaFases.FirstOrDefault(static f => f.ColetaSolicitacaoIsencao)?.Id;

    private static Guid IncluirFaseDeIsencao(ProcessoSeletivo processo)
    {
        int ordem = processo.CronogramaFases.Select(static f => f.Ordem).DefaultIfEmpty(0).Max() + 1;
        (DateTimeOffset inicio, DateTimeOffset fim) = JanelaDaIsencao(processo);

        Result cronograma = processo.DefinirCronogramaFases([.. processo.CronogramaFases, FaseDeIsencaoDaTaxa(ordem, inicio, fim)], []);
        if (cronograma.IsFailure)
        {
            throw new InvalidOperationException($"A fase de isenção de teste não pôde entrar no cronograma: {cronograma.Error?.Message}");
        }

        return FaseDaIsencao(processo)!.Value;
    }

    // A janela abre com a inscrição, fecha um dia antes dela e dura mais de cinco dias corridos quando a inscrição dura o bastante.
    private static (DateTimeOffset Inicio, DateTimeOffset Fim) JanelaDaIsencao(ProcessoSeletivo processo) =>
        processo.CronogramaFases.FirstOrDefault(static f => f.ColetaInscricao && f.Inicio is not null && f.Fim is not null) is { } inscricao
            ? (inscricao.Inicio!.Value, inscricao.Fim!.Value.AddDays(-1))
            : (InicioQuandoOProcessoNaoTemInscricao, InicioQuandoOProcessoNaoTemInscricao.AddDays(10));
}

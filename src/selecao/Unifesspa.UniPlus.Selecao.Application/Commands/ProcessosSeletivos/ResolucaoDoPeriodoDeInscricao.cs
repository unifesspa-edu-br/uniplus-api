namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Resolve o período de inscrição do Edital a partir de uma fonte única (issue #1350):
/// a janela da fase do cronograma que coleta inscrição, quando o certame coleta; o que o
/// operador informou, quando não coleta.
/// </summary>
/// <remarks>
/// A distinção entre "o operador informou" e "o sistema projetou" mora aqui, e não no
/// <see cref="DadosEdital"/>: o value object recebe o período já resolvido e não teria como
/// saber de onde ele veio. É por isso que o command traz o par anulável.
/// </remarks>
internal static class ResolucaoDoPeriodoDeInscricao
{
    /// <returns>
    /// O edital com o período resolvido, ou nulo quando a fase que ancora o período está sem janela: não há período a
    /// resolver, e a conferência da configuração recusa a fase.
    /// </returns>
    internal static Result<DadosEdital>? Resolver(
        ProcessoSeletivo processo,
        string? numero,
        DateTimeOffset? periodoInscricaoInicioInformado,
        DateTimeOffset? periodoInscricaoFimInformado,
        Guid documentoEditalId,
        IReadOnlyList<AnexoDoEdital> anexos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        FaseCronograma? ancora = ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao(processo.Declaracao());
        (DateTimeOffset? Inicio, DateTimeOffset? Fim) informado = (periodoInscricaoInicioInformado, periodoInscricaoFimInformado);

        return ancora is null
            ? SemFaseDeColeta(numero, informado, documentoEditalId, anexos)
            : DaJanelaDaFase(ancora, numero, informado, documentoEditalId, anexos);
    }

    // Sem fase de coleta não há divergência possível, e o período continua sendo o que o ato declara: é o caso do certame
    // cujos candidatos vêm de importação externa.
    private static Result<DadosEdital> SemFaseDeColeta(
        string? numero, (DateTimeOffset? Inicio, DateTimeOffset? Fim) informado, Guid documentoEditalId, IReadOnlyList<AnexoDoEdital> anexos) =>
        informado is { Inicio: { } inicio, Fim: { } fim }
            ? DadosEdital.Criar(numero, inicio, fim, documentoEditalId, anexos)
            : Result<DadosEdital>.Failure(new DomainError(
                "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
                "O processo não tem fase do cronograma que colete inscrição, então o período de inscrição precisa ser informado na publicação."));

    private static Result<DadosEdital>? DaJanelaDaFase(
        FaseCronograma ancora, string? numero, (DateTimeOffset? Inicio, DateTimeOffset? Fim) informado, Guid documentoEditalId, IReadOnlyList<AnexoDoEdital> anexos)
    {
        if (informado.Inicio is not null || informado.Fim is not null)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "ProcessoSeletivo.PeriodoInscricaoNaoInformavel",
                $"O período de inscrição vem da janela da fase '{ancora.Codigo}' do cronograma e não pode ser informado na publicação."));
        }

        return ancora is { Inicio: { } inicio, Fim: { } fim }
            ? DadosEdital.Criar(numero, inicio, fim, documentoEditalId, anexos)
            : null;
    }
}

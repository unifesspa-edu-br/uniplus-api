namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
    internal static Result<DadosEdital> Resolver(
        ProcessoSeletivo processo,
        string? numero,
        DateTimeOffset? periodoInscricaoInicioInformado,
        DateTimeOffset? periodoInscricaoFimInformado,
        Guid documentoEditalId)
    {
        ArgumentNullException.ThrowIfNull(processo);

        FaseCronograma? ancora = processo.FaseQueAncoraOPeriodoDeInscricao();

        if (ancora is null)
        {
            // Sem fase de coleta não há divergência possível, e o período continua sendo o que
            // o ato declara — é o caso do certame cujos candidatos vêm de importação externa.
            if (periodoInscricaoInicioInformado is not { } inicioInformado
                || periodoInscricaoFimInformado is not { } fimInformado)
            {
                return Result<DadosEdital>.Failure(new DomainError(
                    "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
                    "O processo não tem fase do cronograma que colete inscrição, então o período de inscrição precisa ser informado na publicação."));
            }

            return DadosEdital.Criar(numero, inicioInformado, fimInformado, documentoEditalId);
        }

        if (periodoInscricaoInicioInformado is not null || periodoInscricaoFimInformado is not null)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "ProcessoSeletivo.PeriodoInscricaoNaoInformavel",
                $"O período de inscrição vem da janela da fase '{ancora.Codigo}' do cronograma e não pode ser informado na publicação."));
        }

        if (ancora.Inicio is not { } inicio || ancora.Fim is not { } fim)
        {
            throw new InvalidOperationException(
                $"A fase '{ancora.Codigo}' coleta inscrição e está sem janela — PendenciaDoCronograma deveria ter recusado a transição antes deste ponto.");
        }

        return DadosEdital.Criar(numero, inicio, fim, documentoEditalId);
    }
}

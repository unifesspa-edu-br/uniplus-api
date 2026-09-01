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
/// Antes desta issue o período era informado no ato de publicação <b>e</b> definido na janela
/// da fase, sem nada conciliando os dois — um Edital podia declarar inscrições até 15/04
/// enquanto a fase de coleta fechava em 20/03, e as duas versões iam congeladas para o mesmo
/// snapshot.
/// <para>
/// A distinção entre "o operador informou" e "o sistema projetou" mora aqui, e não no
/// <see cref="DadosEdital"/>: o value object recebe o período já resolvido e não teria como
/// saber de onde ele veio. É por isso que o command traz o par anulável.
/// </para>
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
        bool informou = periodoInscricaoInicioInformado is not null || periodoInscricaoFimInformado is not null;

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

        if (informou)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "ProcessoSeletivo.PeriodoInscricaoNaoInformavel",
                $"O período de inscrição vem da janela da fase '{ancora.Codigo}' do cronograma e não pode ser informado na publicação."));
        }

        // Defesa em profundidade: a fase que coleta inscrição sem janela é recusada pelo gate de
        // cronograma, que roda antes. Chegar aqui significaria que o gate deixou passar.
        if (ancora.Inicio is not { } inicio || ancora.Fim is not { } fim)
        {
            return Result<DadosEdital>.Failure(new DomainError(
                "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                $"A fase '{ancora.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período."));
        }

        return DadosEdital.Criar(numero, inicio, fim, documentoEditalId);
    }
}

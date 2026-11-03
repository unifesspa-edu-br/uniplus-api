namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

public static class ReferenciaTemporalDosFatos
{
    /// <summary>
    /// A fase do cronograma que ancora o período de inscrição do Edital (issue #1350) — a de
    /// menor <see cref="FaseCronograma.Ordem"/> entre as que coletam inscrição, ou
    /// <see langword="null"/> quando o certame não coleta inscrição pelo sistema.
    /// </summary>
    /// <remarks>
    /// Nada impede mais de uma fase com <see cref="FaseCronograma.ColetaInscricao"/>, e a coleção
    /// preserva a ordem de ENTRADA, não a de <c>Ordem</c> — mesma armadilha que
    /// <see cref="ResolverDataReferenciaFatos"/> documenta: o envelope escreve as fases ordenadas
    /// por <c>Ordem</c>, então escolher posicionalmente faria a reidratação eleger outra fase e
    /// quebrar o round-trip. <c>Ordem</c> é única por processo, o que torna a escolha determinística.
    /// <para>
    /// Havendo duas janelas de coleta, o período publicado é o da primeira. É consequência aceita:
    /// a segunda janela (tipicamente remanejamento) não estende o período declarado no Edital.
    /// </para>
    /// </remarks>
    public static FaseCronograma? FaseQueAncoraOPeriodoDeInscricao(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return estado.CronogramaFases
            .Where(static f => f.ColetaInscricao)
            .OrderBy(static f => f.Ordem)
            .FirstOrDefault();
    }
}

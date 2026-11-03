namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class RegrasDasExigencias
{
    /// <summary>
    /// Gate <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c> (Story #920) — mesmo gate
    /// para folha e grupo, forward (aqui, na escrita da exigência) e reverso (em
    /// <see cref="DefinirCronogramaFases"/>, via <see cref="ExisteConsequenciaPendenciaReenvioNaFase"/>).
    /// </summary>
    internal static DomainError? ValidarConsequenciaPendenciaReenvio(EstadoDaConfiguracao estado, Guid faseId)
    {
        FaseCronograma? fase = estado.CronogramaFases.FirstOrDefault(f => f.Id == faseId);
        if (fase is null)
        {
            // FaseNaoPertenceAoProcesso já cobre a ausência — defesa em profundidade, não
            // deveria ocorrer (a fase já foi validada acima nesta mesma chamada).
            return null;
        }

        return fase.PermiteComplementacao
            ? null
            : new DomainError(
                "DocumentoExigido.PendenciaReenvioExigeComplementacao",
                $"A fase '{fase.Codigo}' (ordem {fase.Ordem}) não permite complementação — consequência PENDENCIA_REENVIO exige PermiteComplementacao.");
    }

    /// <summary>Lado reverso do gate acima — usado por <see cref="DefinirCronogramaFases"/> ao verificar se uma fase pode perder <c>PermiteComplementacao</c>, para folha OU grupo <c>OU</c>/<c>N-de</c>.</summary>
    internal static bool ExisteConsequenciaPendenciaReenvioNaFase(EstadoDaConfiguracao estado, Guid faseId) =>
        estado.DocumentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO")
        || estado.NosExigencia.Any(n => n.Tipo == TipoNo.GrupoOu && n.Consequencia == "PENDENCIA_REENVIO" && n.FaseComum() == faseId);
}

namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Uma exigência de cobertura: o que o processo precisa ter escolhido de motivos para que as
/// decisões que ele produz tenham com que se justificar. Cada decisão que passa a exigir motivo
/// entra por uma exigência nova, registrada em <see cref="ExigenciasDeMotivos"/>.
/// </summary>
public interface IExigenciaDeMotivos
{
    /// <summary>As faltas do processo diante dos motivos <paramref name="escolhidos"/>; vazio quando a exigência está atendida.</summary>
    IEnumerable<FieldError> Faltas(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos);
}

namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma exigência de cobertura: o que o processo precisa ter escolhido de motivos, em cada fase ou etapa que declara o tipo
/// de decisão, para que as decisões que ele produz tenham com que se justificar. Cada decisão que passa a exigir motivo
/// entra por uma exigência nova, registrada em <see cref="ExigenciasDeMotivos"/>.
/// </summary>
public interface IExigenciaDeMotivos
{
    /// <summary>
    /// As faltas do processo diante dos motivos <paramref name="escolhidos"/>, que já são só os que a versão leva e que o
    /// lugar admite; vazio quando a exigência está atendida.
    /// </summary>
    IEnumerable<FieldError> Faltas(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDoProcesso> escolhidos);
}

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
    /// As faltas de tipo de decisão não declarado: a decisão que o processo exige e nenhum lugar declara produzir, salvo quando a
    /// leitura recusou parte do que declara os tipos. Antecedem, em todas as exigências, a cobertura.
    /// </summary>
    IEnumerable<FieldError> TiposNaoDeclarados(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares);

    /// <summary>
    /// As faltas de cobertura do processo diante dos motivos <paramref name="escolhidos"/>, que já são só os que a versão leva e
    /// que o lugar admite; vazio quando a exigência está atendida.
    /// </summary>
    IEnumerable<FieldError> FaltasDeCobertura(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDeDecisaoDoProcesso> escolhidos);
}

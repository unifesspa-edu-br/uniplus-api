namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A cobertura que a publicação e a retificação exigem: o processo só congela com os motivos
/// que as suas decisões pedem, em cada fase ou etapa que as produz. Cada exigência vive em
/// <see cref="ExigenciasDeMotivos"/>; as faltas de todas elas são agregadas na ordem do registro.
/// </summary>
internal static class CoberturaDosMotivos
{
    /// <summary>As faltas de cada exigência diante dos motivos que a versão leva; vazio quando a cobertura está completa.</summary>
    internal static IEnumerable<FieldError> Faltas(
        ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDoProcesso> escolhidos) =>
        Faltas(configuracao, lugares, escolhidos, ExigenciasDeMotivos.Todas);

    internal static IEnumerable<FieldError> Faltas(
        ConfiguracaoDeclarada configuracao,
        LugaresDaDecisao lugares,
        IReadOnlyCollection<MotivoDoProcesso> escolhidos,
        IEnumerable<IExigenciaDeMotivos> exigencias)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);
        ArgumentNullException.ThrowIfNull(escolhidos);
        ArgumentNullException.ThrowIfNull(exigencias);

        return exigencias.SelectMany(exigencia => exigencia.Faltas(configuracao, lugares, escolhidos));
    }
}

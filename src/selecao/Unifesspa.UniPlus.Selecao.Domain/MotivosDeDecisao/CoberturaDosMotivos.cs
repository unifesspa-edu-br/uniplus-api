namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A cobertura que a publicação e a retificação exigem: o processo só congela com os motivos
/// que as suas decisões pedem, em cada fase ou etapa que as produz. Cada exigência vive em
/// <see cref="ExigenciasDeMotivos"/>. Primeiro vêm as faltas de tipo de decisão não declarado de todas as exigências, e depois as
/// faltas de cobertura de todas, cada grupo na ordem do registro.
/// </summary>
internal static class CoberturaDosMotivos
{
    /// <summary>As faltas das exigências diante dos motivos que a versão leva, na ordem dos tipos não declarados e da cobertura; vazio quando a cobertura está completa.</summary>
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

        IExigenciaDeMotivos[] registradas = [.. exigencias];

        return registradas.SelectMany(exigencia => exigencia.TiposNaoDeclarados(configuracao, lugares))
            .Concat(registradas.SelectMany(exigencia => exigencia.FaltasDeCobertura(configuracao, lugares, escolhidos)));
    }
}

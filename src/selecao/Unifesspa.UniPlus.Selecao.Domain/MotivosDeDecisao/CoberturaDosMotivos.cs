namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Portão da publicação, da retificação e do fechamento da sessão: o processo só congela com os
/// motivos que as suas decisões exigem. Cada exigência vive em <see cref="ExigenciasDeMotivos"/>;
/// as faltas de todas elas são agregadas na ordem do registro.
/// </summary>
public static class CoberturaDosMotivos
{
    /// <summary>Sucesso quando a cobertura está completa; senão, uma recusa por falta de cada exigência.</summary>
    public static Result Conferir(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos) =>
        Conferir(processo, escolhidos, ExigenciasDeMotivos.Todas);

    internal static Result Conferir(
        ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos, IEnumerable<IExigenciaDeMotivos> exigencias)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhidos);

        FieldError[] faltas = [.. exigencias.SelectMany(e => e.Faltas(processo, escolhidos))];
        return faltas.Length > 0 ? Result.ValidationFailure(faltas) : Result.Success();
    }
}

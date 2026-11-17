namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>As exigências de cobertura conferidas sobre os motivos escolhidos, na ordem em que as faltas são devolvidas.</summary>
public static class ExigenciasDeMotivos
{
    public static IReadOnlyList<IExigenciaDeMotivos> Todas { get; } =
    [
        new ExigenciaDaIsencaoDaTaxa(),
    ];
}

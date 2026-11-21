namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Como as etapas do tipo são avaliadas: os caracteres de etapa que o tipo admite, se a correção é independente e os
/// conceitos admitidos. É o mesmo grupo que a leitura devolve em <c>avaliacao</c>, sem <c>notaDeOrigemNoEnem</c>, que só
/// a carga do cadastro define.
/// </summary>
/// <remarks>
/// Os três sinalizadores são anuláveis (ADR-0125): <c>bool</c> não-anulável faria campo ausente virar <c>false</c> em
/// silêncio, e o tipo nasceria declarando que não compõe a nota final sem ninguém ter dito isso. A lista de conceitos é
/// obrigatória, ainda que vazia: ausente é recusada, e não lida como "nenhum conceito". Um tipo sem
/// <c>avaliacao</c> equivale a um com todos os campos ausentes.
/// </remarks>
/// <param name="AdmitePontuacao">Se as etapas do tipo compõem a nota final.</param>
/// <param name="AdmiteEliminacao">Se as etapas do tipo eliminam candidatos.</param>
/// <param name="ExigeCorrecaoIndependente">Se as etapas do tipo são corrigidas por mais de um corretor, cada um independente dos outros.</param>
/// <param name="ConceitosAdmitidos">Os códigos dos tipos de conceito com que as etapas do tipo podem ser avaliadas.</param>
public sealed record AvaliacaoDasEtapasInput(
    bool? AdmitePontuacao,
    bool? AdmiteEliminacao,
    bool? ExigeCorrecaoIndependente,
    IReadOnlyList<string>? ConceitosAdmitidos)
{
    /// <summary>A avaliação de um tipo enviado sem ela: todos os campos ausentes.</summary>
    public static AvaliacaoDasEtapasInput Ausente { get; } = new(null, null, null, null);
}

/// <summary>
/// Leva as recusas dos campos da avaliação ao caminho em que o corpo os recebe, <c>avaliacao.&lt;campo&gt;</c>; as do
/// código, do nome e da descrição, que ficam na raiz, não mudam.
/// </summary>
internal static class CaminhoDaAvaliacao
{
    private static readonly string[] CamposDaRaiz = ["codigo", "nome", "descricao"];

    public static FieldError[] Aplicar(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static erro => erro.Field is { } campo && CamposDaRaiz.Contains(campo, StringComparer.Ordinal) ? erro : erro.NoCaminhoDe("avaliacao"))];
}

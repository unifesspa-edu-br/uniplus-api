namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="ClasseNoGrafo"/> e o token de <c>tipoDeNo</c> UPPER_SNAKE que
/// entra na identidade de nó do bloco <c>grafoDependencia</c> do envelope (Story #928, §7). É a mesma
/// gramática de <c>tipoDeNo</c> de <see cref="ValueObjects.IdCanonico"/> — separada aqui porque a
/// identidade congelada no envelope é <b>escopada ao processo por construção</b> (o envelope pertence
/// a um único processo, <c>VersaoConfiguracao.ProcessoSeletivoId</c>): o nó é <c>tipoDeNo/codigo</c>,
/// sem repetir o Id do processo em cada nó — que seria redundante e congelaria o Guid volátil da raiz
/// nos bytes da configuração.
/// </summary>
public static class ClasseNoGrafoCodigo
{
    public const string Campo = "CAMPO";
    public const string Fato = "FATO";
    public const string Exigencia = "EXIGENCIA";

    /// <summary>
    /// Converte a classe do nó para o token de <c>tipoDeNo</c>. O <c>switch</c> é exaustivo: uma 4ª
    /// classe quebra a build (CS8509 promovido a erro por <c>TreatWarningsAsErrors</c>) até este
    /// mapeamento absorvê-la.
    /// </summary>
    public static string ToCodigo(this ClasseNoGrafo classe) => classe switch
    {
        ClasseNoGrafo.Campo => Campo,
        ClasseNoGrafo.Fato => Fato,
        ClasseNoGrafo.Exigencia => Exigencia,
        _ => throw new ArgumentOutOfRangeException(nameof(classe), classe, "Classe de nó do grafo desconhecida."),
    };
}

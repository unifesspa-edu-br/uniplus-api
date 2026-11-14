namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Leitor cross-módulo do catálogo de tipos de ato (ADR-0056). Deixa o domínio que vai
/// publicar conferir, ANTES de escrever, o tipo que o operador declarou.
/// </summary>
/// <remarks>
/// O domínio confere o tipo antes de escrever porque o registro do ato (<see cref="IRegistroDeAtoNormativo"/>) recebe
/// os atributos do tipo já conferidos, por valor: um tipo inexistente, ou um tipo que não congela configuração declarado
/// numa publicação, que é justamente um congelamento, tem de virar 422 antes de qualquer gravação.
/// </remarks>
public interface ITipoAtoPublicadoReader
{
    /// <summary>
    /// Versão vigente do tipo na <paramref name="diaDaPublicacao"/>, ou
    /// <see langword="null"/> quando o catálogo não tem versão vigente do código nessa data
    /// — a mesma resolução que Publicações faz ao registrar.
    /// </summary>
    Task<TipoAtoPublicadoView?> ObterVigenteAsync(
        string codigo,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As versões vigentes na <paramref name="diaDaPublicacao"/> dos tipos de
    /// <paramref name="codigos"/>, numa só leitura. O código sem versão vigente não aparece no
    /// resultado; a resolução é a mesma de <see cref="ObterVigenteAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TipoAtoPublicadoView>> ListarVigentesAsync(
        IReadOnlyCollection<string> codigos,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que o catálogo diz sobre um tipo de ato. São <b>dados</b> — jamais ramos de
/// comportamento: nenhum código compara <see cref="Codigo"/> com um literal (ADR-0103).
/// </summary>
public sealed record TipoAtoPublicadoView(
    string Codigo,
    string Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    bool EhResultado,
    bool PublicavelComoAvulso = false,
    bool ExigeAtoCorrigido = false);

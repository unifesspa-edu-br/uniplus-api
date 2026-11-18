namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using System.Collections.Immutable;

/// <summary>
/// Quem assina o documento da fase, em ordem: os códigos das pessoas, na posição em que o documento as
/// mostra. A ordem é a posição na lista. O nome não faz parte: vem do cadastro na emissão.
/// </summary>
internal sealed record AssinantesDaFase
{
    private AssinantesDaFase(ImmutableArray<string> codigos) => Codigos = codigos;

    internal IReadOnlyList<string> Codigos { get; }

    internal static AssinantesDaFase Criar(IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        return new AssinantesDaFase([.. codigos]);
    }
}

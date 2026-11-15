namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

/// <summary>Compara o arquivo publicado com os códigos que a API emite e descreve cada diferença, apontando o código.</summary>
internal static class DivergenciasDoCatalogoDeErros
{
    internal static IReadOnlyList<string> Comparar(
        IReadOnlyList<EntradaDoCatalogoDeErros> publicadas,
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas)
    {
        Dictionary<string, EntradaDoCatalogoDeErros> noArquivo = PrimeiraDeCadaCodigo(publicadas);
        Dictionary<string, EntradaDoCatalogoDeErros> naApi = PrimeiraDeCadaCodigo(emitidas);

        return
        [
            .. publicadas.GroupBy(static e => e.Codigo, StringComparer.Ordinal).Where(static g => g.Count() > 1)
                .Select(static g => $"O código '{g.Key}' aparece {g.Count()} vezes no arquivo; cada código tem uma só entrada."),
            .. naApi.Values.Where(e => !noArquivo.ContainsKey(e.Codigo))
                .Select(static e => $"O código '{e.Codigo}' ({e.Descricao}) é emitido pela API e não tem entrada no arquivo."),
            .. noArquivo.Values.Where(e => !naApi.ContainsKey(e.Codigo))
                .Select(static e => $"A entrada '{e.Codigo}' ({e.Descricao}) não corresponde a nenhum código emitido pela API."),
            .. noArquivo.Values.Where(e => naApi.TryGetValue(e.Codigo, out EntradaDoCatalogoDeErros? api) && api != e)
                .Select(e => $"O código '{e.Codigo}' diverge: o arquivo tem {e.Descricao}, a API emite {naApi[e.Codigo].Descricao}."),
        ];
    }

    private static Dictionary<string, EntradaDoCatalogoDeErros> PrimeiraDeCadaCodigo(IEnumerable<EntradaDoCatalogoDeErros> entradas) =>
        entradas.GroupBy(static e => e.Codigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);
}

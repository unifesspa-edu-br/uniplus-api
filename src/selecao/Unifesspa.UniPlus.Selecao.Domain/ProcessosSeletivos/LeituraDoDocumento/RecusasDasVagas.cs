namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// As recusas das vagas como partes recusadas do documento: a causa vai para o caminho do item dela e daí para o ponteiro do
/// documento, e o índice de cada lista do caminho é a posição enviada ou, quando a origem a traduz, a posição canônica.
/// </summary>
internal static class RecusasDasVagas
{
    internal static IReadOnlyList<ParteRecusada> Partes(
        string caminho, int posicaoNoDocumento, string caminhoDoItem, IEnumerable<FieldError> causas, OrigemNoDocumento? origem = null)
    {
        ArgumentNullException.ThrowIfNull(causas);

        OrigemNoDocumento traducao = origem ?? OrigemNoDocumento.Identidade;

        return [.. causas.Select(causa => Parte(caminho, posicaoNoDocumento, causa.NoCaminhoDoItem(caminhoDoItem), traducao))];
    }

    private static ParteRecusada Parte(string caminho, int posicaoNoDocumento, FieldError causa, OrigemNoDocumento origem) =>
        new(caminho, null, posicaoNoDocumento, causa with { Field = PonteiroNoDocumento.Converter(causa.Field, origem) });
}

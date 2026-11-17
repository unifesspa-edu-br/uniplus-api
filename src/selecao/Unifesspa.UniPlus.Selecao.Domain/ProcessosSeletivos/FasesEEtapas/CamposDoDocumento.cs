namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using System.Text;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O campo da causa com o nome que o documento da configuração dá a ele. As regras falam o nome do corpo do pedido
/// (<c>fases[i].faseCanonicaId</c>), o mesmo da gravação, que nunca passa por aqui; o documento guarda as fases no bloco do
/// cronograma e dá outro nome a alguns campos. A tradução acontece num lugar só, na saída de quem entrega a causa ao cliente do
/// documento, e a tabela é por posição: o índice de cada colchete não conta para achar o nome.
/// </summary>
internal static class CamposDoDocumento
{
    private const string BlocoDoCronograma = "cronograma.";

    // O trecho que o documento não tem: a causa fica no trecho da etapa que o declara, seja qual for o campo dentro do conceito.
    private const string ConceitoDaEtapa = "etapas[].conceito";

    // O nome do corpo do pedido, com os índices dos colchetes tirados, e o nome que o documento lhe dá.
    private static readonly Dictionary<string, string> Nomes = new(StringComparer.Ordinal)
    {
        ["fases[].faseCanonicaId"] = "fases[].faseCanonicaCodigo",
        ["fases[].faseConcluinteCodigo"] = "fases[].faseConcluinteChave",
        ["fases[].unidadeResponsavel"] = "fases[].unidadeResponsavelCodigo",
        ["fases[].bancasRequeridas[].categoriasDocumentoIds"] = "fases[].bancasRequeridas[].categoriasDocumentoCodigos",
        ["etapas[].faseCodigo"] = "etapas[].faseChave",
        ["etapas[].id"] = "etapas[].chave",
    };

    internal static IReadOnlyList<FieldError> Traduzir(IEnumerable<FieldError> causas)
    {
        ArgumentNullException.ThrowIfNull(causas);

        return [.. causas.Select(static causa => causa with { Field = Traduzir(causa.Field) })];
    }

    internal static string? Traduzir(string? campo)
    {
        if (string.IsNullOrEmpty(campo))
        {
            return campo;
        }

        (string semIndices, List<string> indices) = SemIndices(campo);
        string nome = NoConceitoDaEtapa(semIndices) ? "etapas[]" : Nomes.GetValueOrDefault(semIndices, semIndices);
        string comIndices = ComIndices(nome, indices);

        return semIndices.StartsWith("fases", StringComparison.Ordinal) ? BlocoDoCronograma + comIndices : comIndices;
    }

    private static bool NoConceitoDaEtapa(string semIndices) =>
        semIndices.StartsWith(ConceitoDaEtapa, StringComparison.Ordinal)
        && (semIndices.Length == ConceitoDaEtapa.Length || semIndices[ConceitoDaEtapa.Length] is '.' or '[');

    private static (string SemIndices, List<string> Indices) SemIndices(string campo)
    {
        StringBuilder semIndices = new(campo.Length);
        List<string> indices = [];
        int inicio = 0;
        while (campo.IndexOf('[', inicio) is >= 0 and var abre && campo.IndexOf(']', abre) is >= 0 and var fecha)
        {
            semIndices.Append(campo, inicio, abre + 1 - inicio).Append(']');
            indices.Add(campo[(abre + 1)..fecha]);
            inicio = fecha + 1;
        }

        return (semIndices.Append(campo, inicio, campo.Length - inicio).ToString(), indices);
    }

    private static string ComIndices(string nome, List<string> indices)
    {
        StringBuilder comIndices = new(nome.Length + (indices.Count * 2));
        int proximo = 0;
        foreach (char atual in nome)
        {
            comIndices.Append(atual);
            if (atual == '[' && proximo < indices.Count)
            {
                comIndices.Append(indices[proximo++]);
            }
        }

        return comIndices.ToString();
    }
}

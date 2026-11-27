namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// O ponteiro JSON (RFC 6901) que a causa entrega ao cliente do documento da configuração. As regras falam o nome do corpo do
/// pedido, em notação de pontos e colchetes (<c>fases[1].faseCanonicaId</c>), o mesmo da gravação por dimensão, que nunca passa
/// por aqui; o documento guarda as fases no bloco do cronograma e dá outro nome a alguns campos, e cada item de lista se aponta
/// pela posição dele no documento recebido. A conversão acontece num lugar só, na saída de quem entrega as causas do documento ao
/// cliente, e traduz o índice canônico que a regra emitiu para a posição de origem (<see cref="OrigemNoDocumento"/>); a tabela de
/// nomes é por posição: o índice de cada colchete não conta para achar o nome. Um índice que não é número é defeito de quem
/// emitiu o campo, que tem de apontar o item pela posição canônica dele. A causa que já é ponteiro (a da parte que a leitura
/// recusou) fica como está.
/// </summary>
public static class PonteiroNoDocumento
{
    private const string BlocoDoCronograma = "cronograma.";

    // O trecho que o documento não tem: a causa fica no trecho da etapa que o declara, seja qual for o campo dentro do conceito.
    private const string ConceitoDaEtapa = "etapas[].conceito";

    // O nome do corpo do pedido, com os índices dos colchetes tirados, e o nome que o documento lhe dá.
    private static readonly Dictionary<string, string> Nomes = new(StringComparer.Ordinal)
    {
        // A ordem da fase, que o documento não tem: a posição da fase na lista é a ordem dela, e a causa fica no trecho da fase.
        ["fases[].ordem"] = "fases[]",
        ["fases[].faseCanonicaId"] = "fases[].tipoFaseCodigo",
        ["fases[].regraRecurso.atoAncoraCodigo"] = "fases[].regraRecurso.publicacaoDeReferenciaAtoCodigo",
        ["fases[].unidadeResponsavel"] = "fases[].unidadeResponsavelCodigo",
        ["fases[].bancasRequeridas[].tipoBancaId"] = "fases[].bancasRequeridas[].tipoBancaCodigo",
        ["fases[].bancasRequeridas[].categoriasDocumentoIds"] = "fases[].bancasRequeridas[].categoriasDocumentoCodigos",
        ["fases[].bancasRequeridas[].categoriasDocumentoIds[]"] = "fases[].bancasRequeridas[].categoriasDocumentoCodigos[]",
        ["etapas[].faseCodigo"] = "etapas[].faseChave",
        ["etapas[].id"] = "etapas[].chave",
        ["etapas[].regraConsolidacao"] = "etapas[].regraDeApuracaoDaAvaliacao",

        // A ordem da etapa, que o documento não tem: a posição da etapa na lista é a ordem dela, e a causa fica no trecho da etapa.
        ["etapas[].ordem"] = "etapas[]",
        ["etapas[].tipoEtapaOrigemId"] = "etapas[].tipoEtapaCodigo",
        ["etapas[].bancas[].tipoBancaId"] = "etapas[].bancas[].tipoBancaCodigo",
        ["etapas[].recursos[].ancora"] = "etapas[].recursos[].eventoInicialDoPrazo",
        ["etapas[].recursos[].atoAncoraCodigo"] = "etapas[].recursos[].publicacaoDeReferenciaAtoCodigo",

        // A classificação, com o nome que as regras do bloco dão ao campo: a regra do catálogo, a etapa citada pela chave e a lista
        // dos critérios de desempate, cuja ordem o documento não tem.
        ["classificacao.regraCalculoCodigo"] = "classificacao.regraCalculo",
        ["classificacao.regraArredondamentoCodigo"] = "classificacao.regraArredondamento",
        ["classificacao.regrasEliminacao[].etapaRef"] = "classificacao.regrasEliminacao[].parametros.etapaChave",
        ["classificacao.regrasEliminacao[].areaCodigo"] = "classificacao.regrasEliminacao[].parametros.areaCodigo",
        ["classificacao.criterios[].ordem"] = "classificacao.criteriosDeDesempate[]",
        ["classificacao.criterios[].parametros.etapaRef"] = "classificacao.criteriosDeDesempate[].parametros.etapaChave",
        ["classificacao.criterios[].parametros.fato"] = "classificacao.criteriosDeDesempate[].parametros.codigoDoDado",
        ["formularios[].itens[].fatoCodigo"] = "formularios[].itens[].codigoDoDado",
        ["formularios[].grupos[].subitens[].fatoCodigo"] = "formularios[].grupos[].subitens[].codigoDoDado",
    };

    // A lista que o documento chama por outro nome: o nome do corpo do pedido vale para a lista e para tudo o que está dentro dela.
    private const string ProdutosDaFase = "fases[].produtos";
    private const string ProdutosDaFaseNoDocumento = "fases[].publicacoesPrevistas";
    private const string ProdutosDaEtapa = "etapas[].produtos";
    private const string ProdutosDaEtapaNoDocumento = "etapas[].publicacoesPrevistas";
    private const string ProcedimentosDaFase = "fases[]." + FaseDeclarada.ListaDosProcedimentosDeRecurso;
    private const string ProcedimentosDaFaseNoDocumento = "fases[].regraRecurso.procedimentosDeRecurso";
    private const string ProcedimentosDaEtapa = "etapas[].recursos[].procedimentos";
    private const string ProcedimentosDaEtapaNoDocumento = "etapas[].recursos[].procedimentosDeRecurso";
    private const string CriteriosDeDesempate = "classificacao.criterios";
    private const string CriteriosDeDesempateNoDocumento = "classificacao.criteriosDeDesempate";

    /// <summary>O ponteiro do campo, que fica nulo quando a causa não aponta o documento.</summary>
    public static string? Converter(string? campo, OrigemNoDocumento origem)
    {
        ArgumentNullException.ThrowIfNull(origem);

        return string.IsNullOrEmpty(campo) || campo[0] == '/' ? campo : Ponteiro(NomeNoDocumento(campo, origem));
    }

    /// <summary>As causas com o campo convertido, na mesma ordem e com o mesmo código; a causa sem campo continua sem campo.</summary>
    public static IReadOnlyList<FieldError> Converter(IEnumerable<FieldError> causas, OrigemNoDocumento origem)
    {
        ArgumentNullException.ThrowIfNull(causas);
        ArgumentNullException.ThrowIfNull(origem);

        return [.. causas.Select(causa => causa with { Field = Converter(causa.Field, origem) })];
    }

    private static string NomeNoDocumento(string campo, OrigemNoDocumento origem)
    {
        (string semIndices, List<string> indices) = SemIndices(campo, origem);
        string nome = NoConceitoDaEtapa(semIndices) ? "etapas[]" : Nomes.GetValueOrDefault(semIndices, NomeDaListaRenomeada(semIndices));
        string comIndices = ComIndices(nome, indices);

        return semIndices.StartsWith(CronogramaDoProcesso.ListaDasFases, StringComparison.Ordinal) ? BlocoDoCronograma + comIndices : comIndices;
    }

    private static string NomeDaListaRenomeada(string semIndices) =>
        TraduzirPrefixo(semIndices, ProcedimentosDaFase, ProcedimentosDaFaseNoDocumento)
        ?? TraduzirPrefixo(semIndices, ProcedimentosDaEtapa, ProcedimentosDaEtapaNoDocumento)
        ?? TraduzirPrefixo(semIndices, CriteriosDeDesempate, CriteriosDeDesempateNoDocumento)
        ?? TraduzirPrefixo(semIndices, ProdutosDaFase, ProdutosDaFaseNoDocumento)
        ?? TraduzirPrefixo(semIndices, ProdutosDaEtapa, ProdutosDaEtapaNoDocumento)
        ?? semIndices;

    private static string? TraduzirPrefixo(string semIndices, string origem, string destino) =>
        semIndices.StartsWith(origem, StringComparison.Ordinal)
        && (semIndices.Length == origem.Length || semIndices[origem.Length] is '.' or '[')
            ? destino + semIndices[origem.Length..]
            : null;

    private static bool NoConceitoDaEtapa(string semIndices) =>
        semIndices.StartsWith(ConceitoDaEtapa, StringComparison.Ordinal)
        && (semIndices.Length == ConceitoDaEtapa.Length || semIndices[ConceitoDaEtapa.Length] is '.' or '[');

    private static (string SemIndices, List<string> Indices) SemIndices(string campo, OrigemNoDocumento origem)
    {
        StringBuilder semIndices = new(campo.Length);
        List<string> indices = [];
        int inicio = 0;
        while (campo.IndexOf('[', inicio) is >= 0 and var abre && campo.IndexOf(']', abre) is >= 0 and var fecha)
        {
            semIndices.Append(campo, inicio, abre + 1 - inicio).Append(']');
            indices.Add(PosicaoDeOrigem(campo, abre, campo[(abre + 1)..fecha], origem));
            inicio = fecha + 1;
        }

        return (semIndices.Append(campo, inicio, campo.Length - inicio).ToString(), indices);
    }

    // A lista se nomeia pelo trecho do campo antes do colchete, que já traz a posição canônica de cada item do caminho.
    private static string PosicaoDeOrigem(string campo, int abre, string indice, OrigemNoDocumento origem) =>
        int.TryParse(indice, NumberStyles.None, CultureInfo.InvariantCulture, out int posicaoCanonica)
            ? origem.PosicaoDeOrigem(campo[..abre], posicaoCanonica).ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentException(
                $"O campo '{campo}' aponta um item de lista por '{indice}', que não é a posição canônica dele.", nameof(campo));

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

    // Cada nome e cada índice é um segmento do ponteiro; a barra e o til do nome se escapam (~1 e ~0).
    private static string Ponteiro(string nomeNoDocumento) =>
        string.Concat(Segmentos(nomeNoDocumento).Select(static segmento => "/" + Escapado(segmento)));

    private static string[] Segmentos(string nomeNoDocumento) =>
        nomeNoDocumento.Split(['.', '[', ']'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>O segmento de ponteiro JSON (RFC 6901) com o til escrito <c>~0</c> e a barra <c>~1</c>.</summary>
    public static string Escapado(string segmento)
    {
        ArgumentNullException.ThrowIfNull(segmento);

        return segmento.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    /// <summary>O nome que o segmento de ponteiro JSON (RFC 6901) designa: <c>~1</c> volta a ser a barra e <c>~0</c>, o til, nessa ordem.</summary>
    public static string Desescapado(string segmento)
    {
        ArgumentNullException.ThrowIfNull(segmento);

        return segmento.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }
}

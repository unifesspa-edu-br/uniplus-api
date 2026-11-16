namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O recorte de competência das bancas requeridas por uma fase: quando ele é obrigatório e quando dois
/// recortes se confundem.
/// </summary>
/// <remarks>
/// <para>
/// <b>O recorte só é obrigatório onde o tipo não basta.</b> Com uma banca por tipo, o próprio tipo já
/// diz de quem é o parecer, e exigir o recorte pediria informação que não acrescenta nada. Com mais de
/// uma, o tipo empata: é o recorte que diz qual delas julga renda e qual julga o requisito
/// étnico-racial, e sem ele o certame publica duas linhas iguais onde há dois responsáveis distintos.
/// Recortes IDÊNTICOS entre duas bancas do mesmo tipo caem na mesma recusa por não distinguirem nada: é
/// o defeito que declarar o recorte existe para fechar, e aceitá-los seria cumprir a letra da exigência
/// sem cumprir o efeito.
/// </para>
/// <para>
/// <b>O agrupamento é pelo código congelado, não pelo id de origem.</b> É o código que aparece no
/// edital publicado, e é por ele que quem lê distingue uma banca da outra; dois ids de origem distintos
/// que congelaram o mesmo código produzem, no documento, exatamente a ambiguidade que esta invariante
/// recusa. O critério é estritamente mais forte que o do id: bancas de mesmo id de origem congelam
/// sempre o mesmo código.
/// </para>
/// </remarks>
public static class RecorteDeCompetenciaDasBancas
{
    private const string CategoriaDuplicada = "BancaRequerida.CategoriaDuplicadaNoRecorte";
    private const string RecorteObrigatorioCodigo = "BancaRequerida.RecorteDeCompetenciaObrigatorio";
    private const string RecorteDuplicadoCodigo = "BancaRequerida.RecorteDeCompetenciaDuplicado";

    /// <summary>Os códigos das recusas do recorte, para quem projeta o recorte num item só.</summary>
    internal static readonly IReadOnlySet<string> Codigos =
        new HashSet<string>([CategoriaDuplicada, RecorteObrigatorioCodigo, RecorteDuplicadoCodigo], StringComparer.Ordinal);

    /// <summary>
    /// As violações do recorte de competência das bancas requeridas pela fase, vazio quando cada banca é
    /// identificável dentro dela. Vêm primeiro as de categoria repetida, por índice, e depois as dos
    /// grupos de bancas de mesmo código.
    /// </summary>
    /// <remarks>
    /// <b>Predicado único, quatro chamadores</b>: <see cref="FaseCronograma.Criar"/>,
    /// <see cref="FaseCronograma.Reidratar"/>, o decodificador do envelope e <c>ProcessoSeletivo</c>, no
    /// gate de publicação e no item de conformidade. Escrito uma vez, nenhum deles pode divergir dos
    /// outros. Sem I/O e sem navegação: só o que já está em memória.
    /// </remarks>
    public static IReadOnlyList<FieldError> Violacoes(
        string codigoDaFase,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

        return
        [
            .. CategoriasRepetidas(codigoDaFase, bancasRequeridas),
            .. RecortesDosGruposDeMesmoCodigo(codigoDaFase, bancasRequeridas),
        ];
    }

    private static IEnumerable<FieldError> CategoriasRepetidas(string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        for (int indice = 0; indice < bancasRequeridas.Count; indice++)
        {
            BancaRequerida banca = bancasRequeridas[indice];
            if (banca.RecorteDeCompetencia.Count != banca.ChaveDoRecorte().Count)
            {
                yield return new(CampoDoRecorte(indice), new DomainError(
                    CategoriaDuplicada,
                    $"A banca '{banca.Codigo}' da fase '{codigoDaFase}' declara a mesma categoria de documento mais de uma vez no recorte de competência."));
            }
        }
    }

    // Índices agrupados pelo código congelado, a chave da ambiguidade. Um grupo de um item só não pede
    // recorte algum.
    private static IEnumerable<FieldError> RecortesDosGruposDeMesmoCodigo(
        string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas) =>
        Enumerable.Range(0, bancasRequeridas.Count)
            .GroupBy(i => bancasRequeridas[i].Codigo, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .SelectMany(grupo => RecortesDoGrupoQueNaoDistinguem(codigoDaFase, bancasRequeridas, grupo));

    /// <summary>
    /// Os recortes já vistos no grupo são comparados como CONJUNTO. Concatená-los num texto de assinatura
    /// seria mais barato e dependeria de nenhum código de categoria conter o separador escolhido:
    /// premissa que vale para o cadastro, cujo código é UPPER_SNAKE, mas não para um envelope
    /// decodificado, que traz texto livre dentro do limite da coluna.
    /// </summary>
    private static List<FieldError> RecortesDoGrupoQueNaoDistinguem(
        string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas, IEnumerable<int> indicesDoGrupo)
    {
        List<HashSet<string>> recortesJaDeclarados = [];
        List<FieldError> violacoes = [];

        foreach (int indice in indicesDoGrupo)
        {
            if (ViolacaoDoRecorte(codigoDaFase, bancasRequeridas[indice], indice, recortesJaDeclarados) is { } violacao)
            {
                violacoes.Add(violacao);
            }
        }

        return violacoes;
    }

    /// <summary>
    /// A violação do recorte da banca diante dos já declarados no grupo; quando não há, o recorte passa a
    /// contar como declarado.
    /// </summary>
    private static FieldError? ViolacaoDoRecorte(
        string codigoDaFase, BancaRequerida banca, int indice, List<HashSet<string>> recortesJaDeclarados)
    {
        IReadOnlyList<string> chave = banca.ChaveDoRecorte();
        if (chave.Count == 0)
        {
            return RecorteObrigatorio(codigoDaFase, banca, indice);
        }

        HashSet<string> recorte = new(chave, StringComparer.Ordinal);
        if (recortesJaDeclarados.Exists(anterior => anterior.SetEquals(recorte)))
        {
            return RecorteDuplicado(codigoDaFase, banca, indice, chave);
        }

        recortesJaDeclarados.Add(recorte);
        return null;
    }

    private static FieldError RecorteObrigatorio(string codigoDaFase, BancaRequerida banca, int indice) =>
        new(CampoDoRecorte(indice), new DomainError(
            RecorteObrigatorioCodigo,
            $"A fase '{codigoDaFase}' requer mais de uma banca '{banca.Codigo}' e cada uma precisa declarar as categorias de documento que julga."));

    private static FieldError RecorteDuplicado(string codigoDaFase, BancaRequerida banca, int indice, IReadOnlyList<string> chave) =>
        new(CampoDoRecorte(indice), new DomainError(
            RecorteDuplicadoCodigo,
            $"A fase '{codigoDaFase}' declara duas bancas '{banca.Codigo}' com o mesmo recorte de competência ({string.Join(", ", chave)}) — recortes iguais não distinguem uma banca da outra."));

    internal static string CampoDoRecorte(int indice) => $"bancasRequeridas[{indice}].categoriasDocumentoIds";
}

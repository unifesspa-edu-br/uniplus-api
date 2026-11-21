namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Os candidatos atendidos pelas bancas requeridas por uma fase: quando declará-los é obrigatório e quando
/// duas bancas atendem os mesmos candidatos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declarar os candidatos atendidos só é obrigatório onde o tipo não basta.</b> Com uma banca por
/// tipo, o próprio tipo já diz de quem é o parecer, e exigi-los pediria informação que não acrescenta
/// nada. Com mais de uma, o tipo empata: são os candidatos atendidos que dizem qual delas julga renda e
/// qual julga o requisito étnico-racial, e sem eles o certame publica duas linhas iguais onde há dois
/// responsáveis distintos. Candidatos atendidos IDÊNTICOS entre duas bancas do mesmo tipo caem em
/// recusa própria por não distinguirem nada: é o defeito que declará-los existe para fechar, e aceitá-los
/// seria cumprir a letra da exigência sem cumprir o efeito.
/// </para>
/// <para>
/// <b>O agrupamento é pelo código congelado, não pelo id de origem.</b> É o código que aparece no
/// edital publicado, e é por ele que quem lê distingue uma banca da outra; dois ids de origem distintos
/// que congelaram o mesmo código produzem, no documento, exatamente a ambiguidade que esta invariante
/// recusa. O critério é estritamente mais forte que o do id: bancas de mesmo id de origem congelam
/// sempre o mesmo código.
/// </para>
/// </remarks>
public static class CandidatosAtendidosPelasBancas
{
    private const string CategoriaRepetida = "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos";
    private const string CandidatosAtendidosObrigatoriosCodigo = "BancaRequerida.CandidatosAtendidosObrigatorios";
    private const string CandidatosAtendidosDuplicadosCodigo = "BancaRequerida.CandidatosAtendidosDuplicados";

    /// <summary>Os códigos das recusas dos candidatos atendidos, para quem os projeta num item só.</summary>
    internal static readonly IReadOnlySet<string> Codigos =
        new HashSet<string>([CategoriaRepetida, CandidatosAtendidosObrigatoriosCodigo, CandidatosAtendidosDuplicadosCodigo], StringComparer.Ordinal);

    /// <summary>
    /// As violações dos candidatos atendidos pelas bancas requeridas pela fase, vazio quando cada banca é
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
            .. CandidatosAtendidosDosGruposDeMesmoCodigo(codigoDaFase, bancasRequeridas),
        ];
    }

    private static IEnumerable<FieldError> CategoriasRepetidas(string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        for (int indice = 0; indice < bancasRequeridas.Count; indice++)
        {
            BancaRequerida banca = bancasRequeridas[indice];
            if (banca.CandidatosAtendidos.Count != banca.ChaveDosCandidatosAtendidos().Count)
            {
                yield return new(CampoDosCandidatosAtendidos(indice), new DomainError(
                    CategoriaRepetida,
                    $"A banca '{banca.Codigo}' da fase '{codigoDaFase}' declara a mesma categoria de documento mais de uma vez nos candidatos atendidos."));
            }
        }
    }

    // Índices agrupados pelo código congelado, a chave da ambiguidade. Um grupo de um item só não pede
    // candidato atendido algum.
    private static IEnumerable<FieldError> CandidatosAtendidosDosGruposDeMesmoCodigo(
        string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas) =>
        Enumerable.Range(0, bancasRequeridas.Count)
            .GroupBy(i => bancasRequeridas[i].Codigo, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .SelectMany(grupo => CandidatosAtendidosDoGrupoQueNaoDistinguem(codigoDaFase, bancasRequeridas, grupo));

    /// <summary>
    /// Os candidatos atendidos já vistos no grupo são comparados como CONJUNTO. Concatená-los num texto de assinatura
    /// seria mais barato e dependeria de nenhum código de categoria conter o separador escolhido:
    /// premissa que vale para o cadastro, cujo código é UPPER_SNAKE, mas não para um envelope
    /// decodificado, que traz texto livre dentro do limite da coluna.
    /// </summary>
    private static List<FieldError> CandidatosAtendidosDoGrupoQueNaoDistinguem(
        string codigoDaFase, IReadOnlyList<BancaRequerida> bancasRequeridas, IEnumerable<int> indicesDoGrupo)
    {
        List<HashSet<string>> candidatosAtendidosJaDeclarados = [];
        List<FieldError> violacoes = [];

        foreach (int indice in indicesDoGrupo)
        {
            if (ViolacaoDosCandidatosAtendidos(codigoDaFase, bancasRequeridas[indice], indice, candidatosAtendidosJaDeclarados) is { } violacao)
            {
                violacoes.Add(violacao);
            }
        }

        return violacoes;
    }

    /// <summary>
    /// A violação dos candidatos atendidos da banca diante dos já declarados no grupo; quando não há, eles
    /// passam a contar como declarados.
    /// </summary>
    private static FieldError? ViolacaoDosCandidatosAtendidos(
        string codigoDaFase, BancaRequerida banca, int indice, List<HashSet<string>> candidatosAtendidosJaDeclarados)
    {
        IReadOnlyList<string> chave = banca.ChaveDosCandidatosAtendidos();
        if (chave.Count == 0)
        {
            return CandidatosAtendidosObrigatorios(codigoDaFase, banca, indice);
        }

        HashSet<string> atendidos = new(chave, StringComparer.Ordinal);
        if (candidatosAtendidosJaDeclarados.Exists(anterior => anterior.SetEquals(atendidos)))
        {
            return CandidatosAtendidosDuplicados(codigoDaFase, banca, indice);
        }

        candidatosAtendidosJaDeclarados.Add(atendidos);
        return null;
    }

    private static FieldError CandidatosAtendidosObrigatorios(string codigoDaFase, BancaRequerida banca, int indice) =>
        new(CampoDosCandidatosAtendidos(indice), new DomainError(
            CandidatosAtendidosObrigatoriosCodigo,
            $"Na fase '{codigoDaFase}', a banca '{banca.Codigo}' não define candidatos atendidos, e a fase requer mais de uma banca desse tipo. Defina as categorias de documento de cada banca desse tipo."));

    private static FieldError CandidatosAtendidosDuplicados(string codigoDaFase, BancaRequerida banca, int indice) =>
        new(CampoDosCandidatosAtendidos(indice), new DomainError(
            CandidatosAtendidosDuplicadosCodigo,
            $"Na fase '{codigoDaFase}', a banca '{banca.Codigo}' atende os mesmos candidatos que outra banca do mesmo tipo. Defina categorias de documento diferentes para cada banca desse tipo."));

    internal static string CampoDosCandidatosAtendidos(int indice) => $"bancasRequeridas[{indice}].categoriasDocumentoIds";
}

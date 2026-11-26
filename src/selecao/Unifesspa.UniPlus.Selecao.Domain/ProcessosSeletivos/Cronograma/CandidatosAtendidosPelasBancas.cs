namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// Os candidatos atendidos pelas bancas requeridas por uma fase: quando declará-los é obrigatório e quando a declaração se
/// contradiz.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bancas do mesmo tipo que atendem os mesmos candidatos julgam em paralelo.</b> Formam um grupo
/// (<see cref="GrupoDeCandidatosAtendidos"/>), e repetir o conjunto de categorias entre elas não é defeito: é o que o tipo que exige
/// duas ou mais bancas pede. As bancas sem categoria atendem todos os candidatos e formam um grupo só.
/// </para>
/// <para>
/// <b>Só é recusado o que não deixa saber quem julga quem.</b> Banca sem categoria ao lado de banca com categoria, no mesmo
/// tipo, porque a primeira atende todos os candidatos e a segunda só alguns; e a mesma categoria declarada duas vezes na mesma
/// banca. O agrupamento é pelo tipo, que é o que o edital publicado mostra, e não pelo cadastro de origem do tipo.
/// </para>
/// </remarks>
public static class CandidatosAtendidosPelasBancas
{
    private const string CategoriaRepetida = "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos";
    private const string CandidatosAtendidosObrigatoriosCodigo = "BancaRequerida.CandidatosAtendidosObrigatorios";

    /// <summary>Os códigos das recusas dos candidatos atendidos, para quem os projeta num item só.</summary>
    internal static readonly IReadOnlySet<string> Codigos =
        new HashSet<string>([CategoriaRepetida, CandidatosAtendidosObrigatoriosCodigo], StringComparer.Ordinal);

    /// <summary>
    /// As violações dos candidatos atendidos pelas bancas requeridas pela fase, vazio quando cada banca atende candidatos que se
    /// sabe quais são. Vêm primeiro as de categoria repetida e depois as de banca sem categoria ao lado de banca com categoria,
    /// cada uma por posição.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Predicado único, quatro chamadores</b>: <see cref="FaseCronograma.Criar"/>,
    /// <see cref="FaseCronograma.Reidratar"/>, o decodificador do envelope e <c>ProcessoSeletivo</c>, no
    /// gate de publicação e no item de conformidade. Escrito uma vez, nenhum deles pode divergir dos
    /// outros. Sem I/O e sem navegação: só o que já está em memória.
    /// </para>
    /// <para>
    /// As mensagens não citam o código da fase: o decodificador do envelope o recebe como foi lido, sem conferi-lo contra o
    /// cadastro, e a mensagem não repete texto recebido (ADR-0023). A banca se identifica pelo rótulo do tipo e pela posição.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<FieldError> Violacoes(IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

        IReadOnlyList<BancaDeclarada> declaradas = BancaDeclarada.De([.. bancasRequeridas.Select(static banca => (banca.Tipo, banca.RegraDeDeliberacao, banca.ConsequenciaDaAusencia))]);

        return
        [
            .. IndicesComCategoriaRepetida(bancasRequeridas).Select(indice => CategoriaRepetidaNaBanca(declaradas[indice], indice)),
            .. IndicesSemCategoriasAoLadoDeBancasComCategorias(bancasRequeridas).Select(indice => CandidatosAtendidosObrigatorios(declaradas[indice], indice)),
        ];
    }

    /// <summary>Os tipos de banca cujos candidatos atendidos a fase recusa: o agrupamento das bancas deles não é confiável.</summary>
    internal static IReadOnlySet<TipoDeBanca> TiposRecusados(IReadOnlyList<BancaRequerida> bancasRequeridas) =>
        IndicesComCategoriaRepetida(bancasRequeridas)
            .Concat(IndicesSemCategoriasAoLadoDeBancasComCategorias(bancasRequeridas))
            .Select(indice => bancasRequeridas[indice].Tipo.Codigo)
            .ToHashSet();

    internal static IReadOnlyList<GrupoDeCandidatosAtendidos> Grupos(IReadOnlyList<BancaRequerida> bancasRequeridas) =>
        GrupoDeCandidatosAtendidos.Agrupar([.. bancasRequeridas.Select(static banca => (banca.Tipo.Codigo, banca.ChaveDosCandidatosAtendidos()))]);

    internal static string CampoDosCandidatosAtendidos(int indice) => $"{FaseDeclarada.ListaDasBancasRequeridas}[{indice}].categoriasDocumentoIds";

    private static IEnumerable<int> IndicesComCategoriaRepetida(IReadOnlyList<BancaRequerida> bancasRequeridas) =>
        Enumerable.Range(0, bancasRequeridas.Count)
            .Where(indice => bancasRequeridas[indice].CandidatosAtendidos.Count != bancasRequeridas[indice].ChaveDosCandidatosAtendidos().Count);

    private static IEnumerable<int> IndicesSemCategoriasAoLadoDeBancasComCategorias(IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        IReadOnlyList<GrupoDeCandidatosAtendidos> grupos = Grupos(bancasRequeridas);
        IReadOnlySet<TipoDeBanca> misturados = GrupoDeCandidatosAtendidos.TiposComBancasComESemCategorias(grupos);

        return grupos
            .Where(grupo => grupo.AtendeTodosOsCandidatos && misturados.Contains(grupo.Tipo))
            .SelectMany(static grupo => grupo.Posicoes)
            .Order();
    }

    private static FieldError CategoriaRepetidaNaBanca(BancaDeclarada banca, int indice) =>
        new(CampoDosCandidatosAtendidos(indice), new DomainError(
            CategoriaRepetida,
            $"Na fase, a banca '{banca.Citacao}' declara a mesma categoria de documento mais de uma vez nos candidatos atendidos."));

    private static FieldError CandidatosAtendidosObrigatorios(BancaDeclarada banca, int indice) =>
        new(CampoDosCandidatosAtendidos(indice), new DomainError(
            CandidatosAtendidosObrigatoriosCodigo,
            $"Na fase, bancas do tipo '{banca.Tipo.Codigo.Rotulo()}' definem candidatos atendidos, e a banca '{banca.Citacao}' não define. Defina as categorias de cada banca desse tipo, ou retire-as de todas."));
}

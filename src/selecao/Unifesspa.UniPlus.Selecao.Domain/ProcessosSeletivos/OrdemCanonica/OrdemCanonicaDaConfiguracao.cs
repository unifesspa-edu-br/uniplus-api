namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A ordem em que cada lista da configuração aparece no documento canônico: o serializador do envelope escreve as listas nela e a
/// declaração da configuração (<see cref="ConfiguracaoDeclarada"/>) as entrega nela, de modo que a posição de um item na
/// declaração é a posição dele no documento, e o ponteiro de uma causa (<see cref="ProcessosSeletivos.FasesEEtapas.PonteiroNoDocumento"/>)
/// aponta o mesmo lugar para quem lê o documento. A regra mora aqui, e cada função é a chave da ordem de uma lista: o desempate
/// pelo conteúdo escrito, que só o serializador conhece, continua nele.
/// </summary>
public static class OrdemCanonicaDaConfiguracao
{
    /// <summary>As fases do cronograma, pela ordem declarada e, no empate, pela identidade.</summary>
    public static IOrderedEnumerable<FaseCronograma> Fases(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        return fases.OrderBy(static fase => fase.Ordem).ThenBy(static fase => fase.Id);
    }

    /// <summary>A posição da etapa no documento.</summary>
    public static int ChaveDaEtapa(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        return etapa.Ordem;
    }

    /// <summary>As etapas pela ordem declarada.</summary>
    public static IOrderedEnumerable<EtapaProcesso> Etapas(IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return etapas.OrderBy(ChaveDaEtapa);
    }

    /// <summary>Os atos que a fase publica, pelo código do ato e pelo papel.</summary>
    public static IOrderedEnumerable<ProdutoDaFase> ProdutosDaFase(IEnumerable<ProdutoDaFase> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        return produtos.OrderBy(static produto => ChaveDoAto(produto.AtoCodigo), StringComparer.Ordinal)
            .ThenBy(static produto => ChaveDoPapel(produto.Papel), StringComparer.Ordinal);
    }

    /// <summary>Os atos que a etapa publica, pelo código do ato e pelo papel.</summary>
    public static IOrderedEnumerable<ProdutoDaEtapa> ProdutosDaEtapa(IEnumerable<ProdutoDaEtapa> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        return produtos.OrderBy(static produto => ChaveDoAto(produto.AtoCodigo), StringComparer.Ordinal)
            .ThenBy(static produto => ChaveDoPapel(produto.Papel), StringComparer.Ordinal);
    }

    /// <summary>
    /// Os recursos da etapa, pelo tipo de evento que inicia o prazo, pelo ato do produto em que o prazo se baseia, pela regra, pelo produto e, por último, pela identidade.
    /// </summary>
    public static IOrderedEnumerable<RecursoDaEtapa> RecursosDaEtapa(IEnumerable<RecursoDaEtapa> recursos, IEnumerable<ProdutoDaEtapa> produtos)
    {
        ArgumentNullException.ThrowIfNull(recursos);
        ArgumentNullException.ThrowIfNull(produtos);

        Dictionary<Guid, string> atoDoProduto = produtos.DistinctBy(static produto => produto.Id)
            .ToDictionary(static produto => produto.Id, static produto => $"{ChaveDoAto(produto.AtoCodigo)}\u001f{ChaveDoPapel(produto.Papel)}");

        return recursos.OrderBy(static recurso => recurso.Ancora)
            .ThenBy(recurso => atoDoProduto.GetValueOrDefault(recurso.ProdutoAncoraId, string.Empty), StringComparer.Ordinal)
            .ThenBy(static recurso => HashCanonicalComputer.NormalizeNfc(recurso.Regra.Codigo), StringComparer.Ordinal)
            .ThenBy(static recurso => HashCanonicalComputer.NormalizeNfc(recurso.Regra.Versao), StringComparer.Ordinal)
            .ThenBy(static recurso => recurso.ProdutoAncoraId)
            .ThenBy(static recurso => recurso.Id);
    }

    /// <summary>
    /// As bancas que a fase requer, pelo código do tipo e pela posição dentro dele; <paramref name="banca"/> acha a banca em cada
    /// item. O par é único na fase e decide a ordem sozinho: o id da banca não entra, nem como desempate.
    /// </summary>
    public static IOrderedEnumerable<T> BancasDaFase<T>(IEnumerable<T> itens, Func<T, BancaRequerida> banca)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(banca);

        return itens.OrderBy(item => banca(item).Tipo.Codigo.Codigo(), StringComparer.Ordinal)
            .ThenBy(item => banca(item).PosicaoNoTipo);
    }

    /// <summary>As bancas que a fase requer, pelo código do tipo e pela posição dentro dele.</summary>
    public static IOrderedEnumerable<BancaRequerida> BancasDaFase(IEnumerable<BancaRequerida> bancas) =>
        BancasDaFase(bancas, static banca => banca);

    /// <summary>As bancas da etapa, pelo código do tipo e pela posição dentro dele; o id da banca não entra, nem como desempate.</summary>
    public static IOrderedEnumerable<BancaDaEtapa> BancasDaEtapa(IEnumerable<BancaDaEtapa> bancas)
    {
        ArgumentNullException.ThrowIfNull(bancas);

        return bancas.OrderBy(static banca => banca.Tipo.Codigo.Codigo(), StringComparer.Ordinal)
            .ThenBy(static banca => banca.PosicaoNoTipo);
    }

    /// <summary>As categorias que a banca julga, pelo código.</summary>
    public static IOrderedEnumerable<CategoriaJulgada> CandidatosAtendidos(IEnumerable<CategoriaJulgada> categorias)
    {
        ArgumentNullException.ThrowIfNull(categorias);

        return categorias.OrderBy(static categoria => categoria.Codigo, StringComparer.Ordinal);
    }

    /// <summary>As finalidades que têm formulário no documento, cada uma uma vez, na ordem da finalidade.</summary>
    public static IEnumerable<FinalidadeFormulario> Finalidades(IEnumerable<FinalidadeFormulario> finalidades)
    {
        ArgumentNullException.ThrowIfNull(finalidades);

        return finalidades.Distinct().Order();
    }

    /// <summary>Os formulários do processo, pela finalidade.</summary>
    public static IOrderedEnumerable<FormularioProcesso> Formularios(IEnumerable<FormularioProcesso> formularios)
    {
        ArgumentNullException.ThrowIfNull(formularios);

        return formularios.OrderBy(static formulario => formulario.Finalidade);
    }

    /// <summary>Os campos coletados, pela finalidade e pela ordem dentro dela.</summary>
    public static IOrderedEnumerable<FatoColetado> Campos(IEnumerable<FatoColetado> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        return campos.OrderBy(static campo => campo.Finalidade).ThenBy(static campo => campo.Ordem);
    }

    /// <summary>Os grupos repetíveis, pela finalidade e pela ordem dentro dela.</summary>
    public static IOrderedEnumerable<GrupoColetado> Grupos(IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        return grupos.OrderBy(static grupo => grupo.Finalidade).ThenBy(static grupo => grupo.Ordem);
    }

    private static string ChaveDoAto(string atoCodigo) => HashCanonicalComputer.NormalizeNfc(atoCodigo);

    private static string ChaveDoPapel(PapelDaPublicacaoPrevista? papel) => papel?.ToString() ?? string.Empty;
}

namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Os formulários do processo como uma definição só para o avaliador (ADR-0135): cada seção de cada
/// formulário é uma página, com a exibição da seção, na ordem dos códigos dos formulários e das seções, com os
/// itens e os grupos dela; os termos de todos os formulários; as derivações e os agregados do
/// processo. Uma definição só porque o formulário seguinte cita os fatos e os derivados do
/// anterior, e o avaliador resolve tudo numa ordem.
/// </summary>
/// <remarks>
/// A página e o termo levam o código do formulário no código, porque o código da seção e o do termo só são
/// únicos dentro do formulário. O item sem seção fica numa página sem exibição, com o código do formulário.
/// </remarks>
public static class DefinicaoDoProcesso
{
    public static DefinicaoFormulario Montar(
        IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<FatoColetado> itens,
        IReadOnlyCollection<GrupoColetado> grupos,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyList<DerivacaoDoFato> derivacoes,
        IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(agregados);

        FormularioProcesso[] naOrdemDosCodigos = [.. formularios.OrderBy(static f => f.Codigo, CodigoDoFormulario.OrdemOrdinal)];
        return new DefinicaoFormulario(
            PaginasDosFormularios(naOrdemDosCodigos, itens, grupos),
            TermosDosFormularios(naOrdemDosCodigos, termos),
            derivacoes, agregados);
    }

    private static DefinicaoPagina[] PaginasDosFormularios(
        IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        [.. formularios.SelectMany(formulario => PaginasDoFormulario(
            formulario,
            [.. itens.Where(i => i.FormularioId == formulario.Id)],
            [.. grupos.Where(g => g.FormularioId == formulario.Id)]))];

    private static DefinicaoTermo[] TermosDosFormularios(IEnumerable<FormularioProcesso> formularios, IEnumerable<TermoExigidoFormulario> termos) =>
        [.. formularios.SelectMany(formulario => TermosDoFormulario(formulario, termos))];

    private static IEnumerable<DefinicaoTermo> TermosDoFormulario(FormularioProcesso formulario, IEnumerable<TermoExigidoFormulario> termos) =>
        termos.Where(t => t.FormularioId == formulario.Id).OrderBy(static t => t.Ordem)
            .Select(t => new DefinicaoTermo(CodigoDoTermo(formulario.Codigo, t.Codigo), t.Exibicao, t.Obrigatoriedade));

    /// <summary>O código da página na definição do processo: <c>CODIGO:SECAO</c>, ou só o código do formulário para o item sem seção.</summary>
    public static string CodigoDaPagina(CodigoDoFormulario formulario, string? secao)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return secao is null ? formulario.Valor : $"{formulario.Valor}:{secao}";
    }

    /// <summary>O código do termo na definição do processo: <c>CODIGO:TERMO</c>.</summary>
    public static string CodigoDoTermo(CodigoDoFormulario formulario, string codigo)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return $"{formulario.Valor}:{codigo}";
    }

    /// <summary>Se a página ou o termo, pelo código que tem na definição do processo, é do formulário.</summary>
    public static bool EhDoFormulario(string codigo, CodigoDoFormulario formulario)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(formulario);
        return codigo == formulario.Valor || codigo.StartsWith(formulario.Valor + ":", StringComparison.Ordinal);
    }

    // Provisório até a retirada da finalidade: as operações por finalidade delegam às do código do formulário.
    public static string CodigoDaPagina(FinalidadeFormulario finalidade, string? secao) => CodigoDaPagina(CodigoDe(finalidade), secao);

    public static string CodigoDoTermo(FinalidadeFormulario finalidade, string codigo) => CodigoDoTermo(CodigoDe(finalidade), codigo);

    public static bool EhDaFinalidade(string codigo, FinalidadeFormulario finalidade) => EhDoFormulario(codigo, CodigoDe(finalidade));

    private static CodigoDoFormulario CodigoDe(FinalidadeFormulario finalidade) => CodigoDoFormulario.Criar(finalidade.Codigo()).Value!;

    private static IEnumerable<DefinicaoPagina> PaginasDoFormulario(
        FormularioProcesso formulario, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos)
    {
        PaginaDoFormulario[] secoes = [.. formulario.Paginas.Where(static e => e.Tipo == TipoPaginaFormulario.Secao).OrderBy(static e => e.Ordem)];
        HashSet<string> codigosDasSecoes = new(secoes.Select(static s => s.Codigo), StringComparer.Ordinal);

        if (PaginaDosItensSemSecao(formulario.Codigo, itens, grupos, codigosDasSecoes) is { } semSecao)
        {
            yield return semSecao;
        }

        foreach (PaginaDoFormulario secao in secoes)
        {
            yield return PaginaDaSecao(formulario.Codigo, secao, itens, grupos);
        }
    }

    private static DefinicaoPagina PaginaDaSecao(
        CodigoDoFormulario formulario, PaginaDoFormulario secao, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos) =>
        Pagina(
            CodigoDaPagina(formulario, secao.Codigo),
            secao.Exibicao,
            [.. itens.Where(i => string.Equals(i.PaginaCodigo, secao.Codigo, StringComparison.Ordinal))],
            [.. grupos.Where(g => string.Equals(g.PaginaCodigo, secao.Codigo, StringComparison.Ordinal))]);

    private static DefinicaoPagina? PaginaDosItensSemSecao(
        CodigoDoFormulario formulario, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos, HashSet<string> secoes)
    {
        FatoColetado[] semSecao = [.. itens.Where(i => i.PaginaCodigo is not { } codigo || !secoes.Contains(codigo))];
        GrupoColetado[] gruposSemSecao = [.. grupos.Where(g => g.PaginaCodigo is not { } codigo || !secoes.Contains(codigo))];
        return semSecao.Length > 0 || gruposSemSecao.Length > 0
            ? Pagina(CodigoDaPagina(formulario, null), null, semSecao, gruposSemSecao)
            : null;
    }

    private static DefinicaoPagina Pagina(string codigo, PredicadoDnf? exibicao, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        new(
            codigo,
            exibicao,
            [.. itens.OrderBy(static i => i.Ordem).Select(Item)],
            [.. grupos.OrderBy(static g => g.Ordem).Select(static g => new DefinicaoGrupo(
                g.Codigo, g.Exibicao, g.Obrigatoriedade, g.Minimo, g.Maximo, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(Item)], g.IncluiCandidato))]);

    private static DefinicaoItem Item(FatoColetado fato) =>
        new(fato.FatoCodigo, fato.Exibicao, fato.Obrigatoriedade, fato.Restricoes, fato.Impedimento, fato.Formato);
}

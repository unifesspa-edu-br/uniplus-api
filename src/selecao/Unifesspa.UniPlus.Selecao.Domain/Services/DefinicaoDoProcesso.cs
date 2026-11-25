namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Os formulários do processo como uma definição só para o avaliador (ADR-0135): cada seção de cada
/// formulário é uma etapa, com a exibição da seção, na ordem das finalidades e das seções, com os
/// itens e os grupos dela; os termos de todos os formulários; as derivações e os agregados do
/// processo. Uma definição só porque o formulário seguinte cita os fatos e os derivados do
/// anterior, e o avaliador resolve tudo numa ordem.
/// </summary>
/// <remarks>
/// A etapa e o termo levam a finalidade no código, porque o código da seção e o do termo só são
/// únicos dentro do formulário. O item sem seção fica numa etapa sem exibição da sua finalidade.
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

        return new DefinicaoFormulario(
            [.. FinalidadesDeclaradas(formularios, itens, grupos).SelectMany(finalidade => EtapasDaFinalidade(
                finalidade, formularios.FirstOrDefault(f => f.Finalidade == finalidade),
                [.. itens.Where(i => i.Finalidade == finalidade)], [.. grupos.Where(g => g.Finalidade == finalidade)]))],
            TermosDaDefinicao(termos), derivacoes, agregados);
    }

    private static IEnumerable<FinalidadeFormulario> FinalidadesDeclaradas(
        IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        formularios.Select(static f => f.Finalidade)
            .Concat(itens.Select(static i => i.Finalidade).OfType<FinalidadeFormulario>())
            .Concat(grupos.Select(static g => g.Finalidade).OfType<FinalidadeFormulario>())
            .Distinct().Order();

    private static DefinicaoTermo[] TermosDaDefinicao(IEnumerable<TermoExigidoFormulario> termos) =>
        [.. termos.OrderBy(static t => t.Finalidade).ThenBy(static t => t.Ordem)
            .Select(static t => new DefinicaoTermo(CodigoDoTermo(t.Finalidade!.Value, t.Codigo), t.Exibicao, t.Obrigatoriedade))];

    /// <summary>O código da etapa na definição do processo: a seção, ou a finalidade para o item sem seção.</summary>
    public static string CodigoDaEtapa(FinalidadeFormulario finalidade, string? secao) =>
        secao is null ? finalidade.ToString() : $"{finalidade}:{secao}";

    /// <summary>O código do termo na definição do processo.</summary>
    public static string CodigoDoTermo(FinalidadeFormulario finalidade, string codigo) =>
        $"{finalidade}:{codigo}";

    /// <summary>Se a etapa ou o termo, pelo código que tem na definição do processo, é da finalidade.</summary>
    public static bool EhDaFinalidade(string codigo, FinalidadeFormulario finalidade)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        string prefixo = finalidade.ToString();
        return codigo == prefixo || codigo.StartsWith(prefixo + ":", StringComparison.Ordinal);
    }

    private static IEnumerable<DefinicaoEtapa> EtapasDaFinalidade(
        FinalidadeFormulario finalidade, FormularioProcesso? formulario, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos)
    {
        EtapaFormulario[] secoes = [.. (formulario?.Etapas ?? []).Where(static e => e.Tipo == TipoEtapaFormulario.Secao).OrderBy(static e => e.Ordem)];
        HashSet<string> codigosDasSecoes = new(secoes.Select(static s => s.Codigo), StringComparer.Ordinal);

        if (EtapaDosItensSemSecao(finalidade, itens, grupos, codigosDasSecoes) is { } semSecao)
        {
            yield return semSecao;
        }

        foreach (EtapaFormulario secao in secoes)
        {
            yield return EtapaDaSecao(finalidade, secao, itens, grupos);
        }
    }

    private static DefinicaoEtapa EtapaDaSecao(
        FinalidadeFormulario finalidade, EtapaFormulario secao, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos) =>
        Etapa(
            CodigoDaEtapa(finalidade, secao.Codigo),
            secao.Exibicao,
            [.. itens.Where(i => string.Equals(i.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))],
            [.. grupos.Where(g => string.Equals(g.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))]);

    private static DefinicaoEtapa? EtapaDosItensSemSecao(
        FinalidadeFormulario finalidade, IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos, HashSet<string> secoes)
    {
        FatoColetado[] semSecao = [.. itens.Where(i => i.EtapaCodigo is not { } codigo || !secoes.Contains(codigo))];
        GrupoColetado[] gruposSemSecao = [.. grupos.Where(g => g.EtapaCodigo is not { } codigo || !secoes.Contains(codigo))];
        return semSecao.Length > 0 || gruposSemSecao.Length > 0
            ? Etapa(CodigoDaEtapa(finalidade, null), null, semSecao, gruposSemSecao)
            : null;
    }

    private static DefinicaoEtapa Etapa(string codigo, PredicadoDnf? exibicao, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        new(
            codigo,
            exibicao,
            [.. itens.OrderBy(static i => i.Ordem).Select(Item)],
            [.. grupos.OrderBy(static g => g.Ordem).Select(static g => new DefinicaoGrupo(
                g.Codigo, g.Exibicao, g.Obrigatoriedade, g.Minimo, g.Maximo, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(Item)], g.IncluiCandidato))]);

    private static DefinicaoItem Item(FatoColetado fato) =>
        new(fato.FatoCodigo, fato.Exibicao, fato.Obrigatoriedade, fato.Restricoes, fato.Impedimento, fato.Formato);
}

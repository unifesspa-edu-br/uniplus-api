namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A conferência, sem acesso a dados, da governança com o catálogo que entra: a composição gravada de cada perfil de
/// governança contra a esperada e a classe e o escopo de cada código dela contra o catálogo anterior. Cada diferença é um
/// fato; a governança não se suspende.
/// </summary>
public static class ConferenciaDaGovernanca
{
    /// <summary>As divergências da composição: o que falta e o que sobra em cada perfil de governança.</summary>
    public static IReadOnlyList<DivergenciaDaGovernanca> DaComposicao(
        Guid implantacaoId, IEnumerable<PermissaoDeGovernanca> gravada, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(gravada);

        HashSet<PermissaoDeGovernanca> gravadas = [.. gravada];
        IEnumerable<PermissaoDeGovernanca> faltantes = ComposicaoDaGovernanca.Esperada.Where(p => !gravadas.Contains(p));
        IEnumerable<PermissaoDeGovernanca> excedentes = gravadas.Where(p => !ComposicaoDaGovernanca.Esperada.Contains(p));

        return [.. faltantes.Select(p => Registrar(implantacaoId, p, TipoDeDivergencia.ComposicaoFaltante, instante))
            .Concat(excedentes.Select(p => Registrar(implantacaoId, p, TipoDeDivergencia.ComposicaoExcedente, instante)))];
    }

    /// <summary>
    /// As divergências de classe e de escopo dos códigos da governança entre o catálogo anterior e o novo. Sem catálogo
    /// anterior não há com o que comparar.
    /// </summary>
    public static IReadOnlyList<DivergenciaDaGovernanca> DoCatalogo(
        Guid implantacaoId,
        IReadOnlyDictionary<string, LinhaDoRetratoDoCatalogo> anterior,
        IReadOnlyDictionary<string, LinhaDoRetratoDoCatalogo> novo,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(anterior);
        ArgumentNullException.ThrowIfNull(novo);

        return [.. ComposicaoDaGovernanca.Esperada
            .Where(p => anterior.ContainsKey(p.Codigo) && novo.ContainsKey(p.Codigo))
            .SelectMany(p => MudancasDe(implantacaoId, p, (anterior[p.Codigo], novo[p.Codigo]), instante))];
    }

    private static IEnumerable<DivergenciaDaGovernanca> MudancasDe(
        Guid implantacaoId, PermissaoDeGovernanca permissao, (LinhaDoRetratoDoCatalogo Antes, LinhaDoRetratoDoCatalogo Depois) linhas, DateTimeOffset instante)
    {
        if (linhas.Antes.Classificacao != linhas.Depois.Classificacao)
        {
            yield return DivergenciaDaGovernanca.DaMudanca(
                implantacaoId, permissao.PerfilId, permissao.Codigo, TipoDeDivergencia.Classificacao,
                new ValorDivergente(Texto(linhas.Antes.Classificacao), Texto(linhas.Depois.Classificacao)), instante, 0);
        }

        if (linhas.Antes.DeUnidade != linhas.Depois.DeUnidade)
        {
            yield return DivergenciaDaGovernanca.DaMudanca(
                implantacaoId, permissao.PerfilId, permissao.Codigo, TipoDeDivergencia.Escopo,
                new ValorDivergente(linhas.Antes.DeUnidade.ToString(), linhas.Depois.DeUnidade.ToString()), instante, 0);
        }
    }

    private static string Texto(Unifesspa.UniPlus.Kernel.Domain.Enums.ClassificacaoDeProtecao classificacao) =>
        classificacao.ToString().ToLowerInvariant();

    private static DivergenciaDaGovernanca Registrar(Guid implantacaoId, PermissaoDeGovernanca permissao, TipoDeDivergencia tipo, DateTimeOffset instante) =>
        DivergenciaDaGovernanca.DaComposicao(implantacaoId, permissao.PerfilId, permissao.Codigo, tipo, instante, 0);
}

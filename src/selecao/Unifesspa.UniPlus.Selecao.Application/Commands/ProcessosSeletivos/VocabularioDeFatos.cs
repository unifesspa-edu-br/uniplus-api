namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Leitura do catálogo de fatos do candidato pela Seleção: o tipo de domínio de cada fato para o
/// validador de predicado, e os valores que o PRÓPRIO processo oferece aos fatos cuja fonte não é
/// o catálogo (ADR-0136). Decide pela fonte dos valores declarada no catálogo, nunca pelo código
/// do fato.
/// </summary>
internal static class VocabularioDeFatos
{
    private const string DominioBooleano = "BOOLEANO";
    private const string DominioNumerico = "NUMERICO";
    private const string DominioCategorico = "CATEGORICO";
    private const string FonteGlobal = "GLOBAL";
    private const string FonteProcesso = "PROCESSO";
    private const string FonteModalidade = "MODALIDADE";

    /// <summary>
    /// O tipo de domínio do fato: estático quando os valores estão no catálogo, dinâmico quando
    /// vêm do processo (opções declaradas ou modalidades ofertadas); <see langword="null"/> para
    /// fato fora do vocabulário avaliável.
    /// </summary>
    public static TipoDominioFato? Classificar(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return fato switch
        {
            { Dominio: DominioBooleano } => TipoDominioFato.Booleano,
            { Dominio: DominioNumerico } => TipoDominioFato.Numerico,
            { Dominio: DominioCategorico, FonteValores: FonteGlobal } => TipoDominioFato.CategoricoEstatico,
            { Dominio: DominioCategorico, FonteValores: FonteProcesso or FonteModalidade } => TipoDominioFato.CategoricoDinamico,
            _ => null,
        };
    }

    /// <summary>Indica que as opções do fato são as que o processo declara.</summary>
    public static bool OpcoesDoProcesso(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return fato is { Dominio: DominioCategorico, FonteValores: FonteProcesso };
    }

    /// <summary>
    /// O domínio de cada fato categórico dinâmico do catálogo neste processo: as opções que o
    /// processo declara, ou as modalidades que ele oferta. Uma condição que cite o fato é
    /// validada contra esse conjunto, nunca contra um catálogo global.
    /// </summary>
    public static Dictionary<string, IReadOnlySet<string>> DominiosDinamicos(
        ProcessoSeletivo processo, IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, IReadOnlySet<string>> dominios = new(StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in catalogo)
        {
            switch (fato)
            {
                case { Dominio: DominioCategorico, FonteValores: FonteProcesso }:
                    dominios[fato.Codigo] = new HashSet<string>(
                        processo.OpcoesDoProcesso(fato.Codigo).Select(static o => o.Codigo), StringComparer.Ordinal);
                    break;
                case { Dominio: DominioCategorico, FonteValores: FonteModalidade }:
                    dominios[fato.Codigo] = new HashSet<string>(
                        processo.DistribuicaoVagas.SelectMany(static d => d.Modalidades).Select(static m => m.Codigo),
                        StringComparer.Ordinal);
                    break;
                default:
                    break;
            }
        }

        return dominios;
    }
}

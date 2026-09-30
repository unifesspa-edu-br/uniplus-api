namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text;

using Domain.Entities;
using Domain.Enums;

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
    private const string FonteMunicipiosBonus = "MUNICIPIOS_BONUS";

    /// <summary>
    /// O tipo de domínio do fato: estático quando os valores estão no catálogo, dinâmico quando
    /// vêm do processo (opções declaradas, modalidades ofertadas ou municípios do bônus regional);
    /// <see langword="null"/> para
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
            { Dominio: DominioCategorico, FonteValores: FonteProcesso or FonteModalidade or FonteMunicipiosBonus } => TipoDominioFato.CategoricoDinamico,
            _ => null,
        };
    }

    /// <summary>De onde vêm as opções do fato quando ele é coletado pelo processo.</summary>
    public static OrigemValoresColeta OrigemValores(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return fato switch
        {
            { Dominio: DominioCategorico, FonteValores: FonteProcesso } => OrigemValoresColeta.OpcoesDoProcesso,
            { Dominio: DominioCategorico, FonteValores: FonteMunicipiosBonus } => OrigemValoresColeta.MunicipiosDoBonus,
            _ => OrigemValoresColeta.Catalogo,
        };
    }

    /// <summary>Indica que as opções do fato são as que o processo declara.</summary>
    public static bool OpcoesDoProcesso(FatoCandidatoView fato) =>
        OrigemValores(fato) == OrigemValoresColeta.OpcoesDoProcesso;

    /// <summary>
    /// O domínio de cada fato categórico dinâmico do catálogo neste processo: as opções que o
    /// processo declara, as modalidades que ele oferta ou os municípios do bônus regional. Uma condição que cite o fato é
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
                case { Dominio: DominioCategorico, FonteValores: FonteMunicipiosBonus }:
                    dominios[fato.Codigo] = new HashSet<string>(
                        MunicipiosDoBonus(processo).Select(static m => m.CodigoIbge), StringComparer.Ordinal);
                    break;
                default:
                    break;
            }
        }

        return dominios;
    }

    /// <summary>
    /// Os municípios da área do bônus regional que o processo configurou, em ordem alfabética de
    /// nome (sem distinguir acento nem caixa, para "Água Azul do Norte" vir antes de "Marabá");
    /// vazio quando o processo não tem bônus.
    /// </summary>
    public static IReadOnlyList<ConfiguracaoBonusRegionalMunicipio> MunicipiosDoBonus(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return [.. (processo.BonusRegional?.Municipios ?? [])
            .OrderBy(static m => ChaveAlfabetica(m.Nome), StringComparer.Ordinal)
            .ThenBy(static m => m.Nome, StringComparer.Ordinal)
            .ThenBy(static m => m.CodigoIbge, StringComparer.Ordinal)];
    }

    /// <summary>O nome sem acento e em caixa baixa invariante: chave determinística de ordem alfabética.</summary>
    private static string ChaveAlfabetica(string nome)
    {
        StringBuilder chave = new(nome.Length);
        foreach (char c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                chave.Append(char.ToLowerInvariant(c));
            }
        }

        return chave.ToString();
    }
}

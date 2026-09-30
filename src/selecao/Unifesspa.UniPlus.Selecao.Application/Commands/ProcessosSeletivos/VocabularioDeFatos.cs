namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text;

using Domain.Entities;
using Domain.Enums;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Leitura do catálogo de fatos do candidato pela Seleção: o tipo de domínio de cada fato para o
/// validador de predicado, e os valores que uma condição pode citar quando a fonte não é o catálogo
/// — os que o processo oferece, ou as UFs e os municípios do Geo (ADR-0136). Decide pela fonte dos
/// valores declarada no catálogo, nunca pelo código do fato.
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
    private const string FonteGeoUf = "GEO_UF";
    private const string FonteGeoMunicipio = "GEO_MUNICIPIO";

    /// <summary>
    /// O tipo de domínio do fato: estático quando os valores estão no catálogo; dinâmico quando vêm
    /// do processo (opções declaradas, modalidades ofertadas ou municípios do bônus regional) ou do
    /// Geo (UF e município de residência); <see langword="null"/> para fato fora do vocabulário
    /// avaliável, como texto, data e endereço.
    /// </summary>
    public static TipoDominioFato? Classificar(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return fato switch
        {
            { Dominio: DominioBooleano } => TipoDominioFato.Booleano,
            { Dominio: DominioNumerico } => TipoDominioFato.Numerico,
            { Dominio: DominioCategorico, FonteValores: FonteGlobal } => TipoDominioFato.CategoricoEstatico,
            { Dominio: DominioCategorico, FonteValores: FonteProcesso or FonteModalidade or FonteMunicipiosBonus or FonteGeoUf or FonteGeoMunicipio }
                => TipoDominioFato.CategoricoDinamico,
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
    /// processo declara, as modalidades que ele oferta, os municípios do bônus regional ou, nos
    /// derivados de residência, as UFs e os municípios do Geo. Uma condição que cite o fato é
    /// validada contra esse conjunto, nunca contra um catálogo global.
    /// </summary>
    public static Dictionary<string, DominioDeValores> DominiosDinamicos(
        ProcessoSeletivo processo, IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, DominioDeValores> dominios = new(StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in catalogo)
        {
            switch (fato)
            {
                case { Dominio: DominioCategorico, FonteValores: FonteProcesso }:
                    dominios[fato.Codigo] = DominioDeValores.Enumerado(
                        processo.OpcoesDoProcesso(fato.Codigo).Select(static o => o.Codigo));
                    break;
                case { Dominio: DominioCategorico, FonteValores: FonteModalidade }:
                    dominios[fato.Codigo] = DominioDeValores.Enumerado(
                        processo.DistribuicaoVagas.SelectMany(static d => d.Modalidades).Select(static m => m.Codigo));
                    break;
                case { Dominio: DominioCategorico, FonteValores: FonteMunicipiosBonus }:
                    dominios[fato.Codigo] = DominioDeValores.Enumerado(
                        MunicipiosDoBonus(processo).Select(static m => m.CodigoIbge));
                    break;
                case { Dominio: DominioCategorico, FonteValores: FonteGeoUf }:
                    dominios[fato.Codigo] = DominioDeValores.Enumerado(ReferenciaCidadeGeo.Ufs);
                    break;
                case { Dominio: DominioCategorico, FonteValores: FonteGeoMunicipio }:
                    // Os municípios vêm do Geo, que o servidor não enumera: a condição cita um
                    // código IBGE conferido só pela forma, como toda referência de cidade (ADR-0090).
                    dominios[fato.Codigo] = DominioDeValores.PorFormato(ReferenciaCidadeGeo.EhCodigoMunicipioValido);
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

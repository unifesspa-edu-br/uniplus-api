namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que um predicado sobre fatos do catálogo sabe avaliar, na Configuração: o descritor de cada fato
/// citável e o domínio dos categóricos cujos valores o catálogo não enumera. Compartilhado pelas regras
/// padrão dos derivados e pelos modelos de formulário, que conferem as regras contra o mesmo catálogo.
/// </summary>
public static class VocabularioDoCatalogo
{
    /// <summary>
    /// Os fatos que um predicado sabe avaliar: booleano, numérico e categórico do candidato; o
    /// categórico global só quando já tem valores.
    /// </summary>
    public static Dictionary<string, DescritorFatoCandidato> Descritores(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, DescritorFatoCandidato> vocabulario = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in fatos.Where(static f => f.Escopo == EscopoFato.Candidato))
        {
            TipoDominioFato? tipo = fato switch
            {
                { Dominio: DominioFato.Booleano } => TipoDominioFato.Booleano,
                { Dominio: DominioFato.Numerico } => TipoDominioFato.Numerico,
                { Dominio: DominioFato.Categorico, FonteValores: FonteValoresFato.Global } => TipoDominioFato.CategoricoEstatico,
                { Dominio: DominioFato.Categorico } => TipoDominioFato.CategoricoDinamico,
                _ => null,
            };
            if (tipo is null)
            {
                continue;
            }

            IReadOnlyList<string>? valores = tipo == TipoDominioFato.CategoricoEstatico
                ? [.. fato.ValoresDominioDeclarados.Select(static v => v.Codigo)]
                : null;
            if (DescritorFatoCandidato.Criar(fato.Codigo, tipo.Value, valores) is { IsSuccess: true } descritor)
            {
                vocabulario[fato.Codigo] = descritor.Value!;
            }
        }

        return vocabulario;
    }

    /// <summary>
    /// O domínio dos categóricos cujos valores o catálogo não enumera. UF e município de residência
    /// vêm do Geo e são conferidos como em qualquer processo; as opções do processo, as modalidades
    /// e os municípios do bônus só existem no processo, que confere a regra de novo quando a copia.
    /// </summary>
    public static Dictionary<string, DominioDeValores> DominiosDinamicos(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, DominioDeValores> dominios = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in fatos.Where(static f => f.Dominio == DominioFato.Categorico))
        {
            DominioDeValores? dominio = fato.FonteValores switch
            {
                FonteValoresFato.GeoUf => DominioDeValores.Enumerado(ReferenciaCidadeGeo.Ufs),
                FonteValoresFato.GeoMunicipio => DominioDeValores.PorFormato(ReferenciaCidadeGeo.EhCodigoMunicipioValido),
                FonteValoresFato.Processo or FonteValoresFato.Modalidade or FonteValoresFato.MunicipiosBonus
                    => DominioDeValores.PorFormato(static _ => true),
                _ => null,
            };
            if (dominio is not null)
            {
                dominios[fato.Codigo] = dominio;
            }
        }

        return dominios;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
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
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        Dictionary<string, DescritorFatoCandidato> vocabulario = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in porCodigo.Values.Where(static f => f.Escopo == EscopoFato.Candidato))
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
                ? [.. ValoresDe(fato, porCodigo).Select(static v => v.Codigo)]
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

    /// <summary>O catálogo como as regras do formulário o conferem, por código (<see cref="FatoDoCatalogo"/>).</summary>
    public static Dictionary<string, FatoDoCatalogo> ParaRegras(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        return porCodigo.Values.ToDictionary(
            static f => f.Codigo,
            f => new FatoDoCatalogo(
                f.Codigo,
                DominiosFato.ParaTokenCanonico(f.Dominio),
                CardinalidadesFato.ParaTokenCanonico(f.Cardinalidade),
                OrigensFato.ParaTokenCanonico(f.Origem),
                f.Binding,
                f.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null,
                EscoposFato.ParaTokenCanonico(f.Escopo),
                f.Ativo,
                [.. ValoresDe(f, porCodigo).Select(static v => new ValorDoCatalogo(v.Codigo, v.Ativo))]),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Os valores do fato: os próprios, ou, no agregado sobre grupo repetível, os do fato de membro
    /// que ele resume (ADR-0138) — o agregado não tem valores próprios. A regra única para quem lê o
    /// domínio efetivo de um fato: o vocabulário, a conferência das regras e o contrato que a Seleção
    /// lê. A manutenção do administrador mostra o que cada fato guarda; os valores do agregado são
    /// mantidos no fato de membro.
    /// </summary>
    public static IReadOnlyCollection<FatoValorDominio> ValoresDe(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(porCodigo);
        return fato.FatoDeMembroAgregado is { } membro && porCodigo.TryGetValue(membro, out FatoCandidato? doMembro)
            ? doMembro.ValoresDominioDeclarados
            : fato.ValoresDominioDeclarados;
    }

    private static Dictionary<string, FatoCandidato> PorCodigo(IEnumerable<FatoCandidato> fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    /// <summary>
    /// As dependências de cada derivado por regra que o catálogo sabe derivar: os fatos citados pelas
    /// regras padrão dele. O derivado sem regra padrão não entra — quem o deriva é o processo.
    /// </summary>
    public static Dictionary<string, IReadOnlyCollection<string>> Derivacoes(IEnumerable<FatoCandidato> fatos) =>
        RegrasDeDerivacao(fatos).ToDictionary(static r => r.CodigoFato, static r => r.DependenciasDeclaradas, StringComparer.Ordinal);

    /// <summary>O formato da resposta de cada fato de texto, em token canônico.</summary>
    public static Dictionary<string, string> Formatos(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return fatos
            .Where(static f => f.Formato is { } formato && formato != FormatoTexto.Nenhum)
            .ToDictionary(static f => f.Codigo, static f => FormatosTexto.ParaTokenCanonico(f.Formato!.Value), StringComparer.Ordinal);
    }

    /// <summary>
    /// As regras de derivação que o catálogo sabe aplicar: as regras padrão de cada derivado por
    /// regra, booleano ou categórico, com as dependências citadas por elas. O catálogo as conferiu ao
    /// gravá-las, então uma recusa aqui é defeito.
    /// </summary>
    public static IReadOnlyList<RegrasDerivacaoFato> RegrasDeDerivacao(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return [.. fatos
            .Where(static f => f.RegrasPadrao.Count > 0 && VinculoDeFato.Usa(f.Binding, VinculoDeFato.RegraDeDerivacao))
            .Select(static f =>
            {
                string[] dependencias = [.. f.RegrasPadrao.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];
                Result<RegrasDerivacaoFato> regras = f.Dominio == DominioFato.Booleano
                    ? RegrasDerivacaoFato.CriarBooleana(f.Codigo, f.RegrasPadrao, dependencias)
                    : RegrasDerivacaoFato.Criar(f.Codigo, f.RegrasPadrao, dependencias, [.. f.ValoresDominioDeclarados.Select(static v => v.Codigo)]);
                return regras.IsSuccess
                    ? regras.Value!
                    : throw new InvalidOperationException($"As regras padrão gravadas de '{f.Codigo}' não formam uma derivação: {regras.Error!.Message}");
            })];
    }
}

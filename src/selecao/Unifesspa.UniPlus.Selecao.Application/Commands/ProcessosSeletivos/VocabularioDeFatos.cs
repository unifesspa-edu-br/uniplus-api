namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Leitura do catálogo de fatos do candidato pela Seleção: o tipo de domínio de cada fato para o
/// validador de predicado, e os valores que uma condição pode citar quando a fonte não é o catálogo
/// — os que o processo oferece, ou as UFs e os municípios do Geo (ADR-0136). Decide pela fonte dos
/// valores declarada no catálogo, nunca pelo código do fato.
/// </summary>
internal static class VocabularioDeFatos
{
    internal const string DominioBooleano = "BOOLEANO";
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

    /// <summary>
    /// O vocabulário que o validador de predicado usa: o descritor de cada fato avaliável do
    /// catálogo, por código. Texto, data e endereço ficam de fora.
    /// </summary>
    public static Dictionary<string, DescritorFatoCandidato> Descritores(IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return catalogo
            .Select(static fato => Classificar(fato) is { } tipo ? DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio) : null)
            .Where(static descritor => descritor is { IsSuccess: true })
            .Select(static descritor => descritor!.Value!)
            .ToDictionary(static descritor => descritor.Codigo, StringComparer.Ordinal);
    }

    /// <summary>
    /// O fato como as regras do formulário o conferem contra o catálogo; os valores com o estado de
    /// cada um, ou os códigos do domínio, ativos, quando o catálogo não traz a descrição por valor.
    /// </summary>
    public static FatoDoCatalogo ParaRegras(FatoCandidatoView fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return new FatoDoCatalogo(
            fato.Codigo, fato.Dominio, fato.Cardinalidade, fato.Origem, fato.Binding, fato.FonteValores, fato.Escopo, fato.Ativo,
            fato.ValoresDominioDeclarados is { } declarados
                ? [.. declarados.Select(static v => new ValorDoCatalogo(v.Codigo, v.Ativo))]
                : [.. (fato.ValoresDominio ?? []).Select(static v => new ValorDoCatalogo(v, Ativo: true))]);
    }

    /// <summary>O catálogo como as regras do formulário o conferem, por código.</summary>
    public static Dictionary<string, FatoDoCatalogo> ParaRegras(IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return catalogo.ToDictionary(static f => f.Codigo, ParaRegras, StringComparer.Ordinal);
    }

    /// <summary>
    /// A recusa de citar, numa regra do formulário, fato que o sistema calcula de atributos do
    /// candidato (<see cref="ConferenciaNoCatalogo.CitacaoDeAtributoDoCandidato"/>).
    /// </summary>
    public static DomainError? CitacaoDeAtributoDoCandidato(
        IEnumerable<string> citados, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return ConferenciaNoCatalogo.CitacaoDeAtributoDoCandidato(citados, ParaRegras(catalogo.Values));
    }

    /// <summary>Se o vínculo é de fato que o sistema calcula de atributos do candidato, como a faixa etária.</summary>
    public static bool CalculadoDeAtributo(string binding) => VinculoDeFato.Usa(binding, VinculoDeFato.AtributoDoCandidato);

    /// <summary>Se o vínculo é de fato que a classificação produz, como o grupo em que o candidato foi convocado.</summary>
    public static bool ProduzidoPelaClassificacao(string binding) => VinculoDeFato.Usa(binding, VinculoDeFato.Classificacao);

    /// <summary>O fato de membro de cada agregado sobre grupo repetível do catálogo, pelo código do agregado.</summary>
    public static Dictionary<string, string> MembroPorAgregado(IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return catalogo
            .Select(static f => (f.Codigo, Membro: VinculoDeFato.Nomeado(f.Binding, VinculoDeFato.AgregacaoDeGrupo)))
            .Where(static a => a.Membro is not null)
            .ToDictionary(static a => a.Codigo, static a => a.Membro!, StringComparer.Ordinal);
    }

    /// <summary>
    /// Os fatos que o processo resolve para um candidato — o universo contra o qual um gatilho é
    /// conferido.
    /// </summary>
    /// <remarks>
    /// São cinco conjuntos, e omitir qualquer um recusa configuração legítima: o que o processo
    /// coleta nos formulários; o que ele deriva por regra declarada (a modalidade de
    /// concorrência); o agregado cujo fato de membro é campo de um grupo do processo; o que o
    /// sistema calcula de atributos do candidato (faixa etária, renda per capita) — o que declara
    /// dependências, só quando o processo coleta todas elas, porque sem a data de nascimento a faixa
    /// etária nunca se resolve; e o que a classificação produz (o grupo em que o candidato foi
    /// convocado). Os dois últimos nunca aparecem nas regras de derivação do processo.
    /// </remarks>
    public static HashSet<string> QueOProcessoResolve(ProcessoSeletivo processo, IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        List<FatoCandidatoView> fatos = [.. catalogo];
        HashSet<string> camposDeGrupo = new(
            processo.GruposColetados.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo), StringComparer.Ordinal);
        HashSet<string> coletados = new(processo.FatosColetados.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        HashSet<string> resolvidos = new(DerivadosDoSistemaResolvidos(coletados), StringComparer.Ordinal);
        bool DependenciasColetadas(string codigo) => !DerivadosDoSistema.Dependencias.ContainsKey(codigo) || resolvidos.Contains(codigo);
        return new(
            coletados
                .Concat(processo.RegrasDerivacao.Select(static r => r.CodigoFato))
                .Concat(MembroPorAgregado(fatos).Where(a => camposDeGrupo.Contains(a.Value)).Select(static a => a.Key))
                .Concat(fatos
                    .Where(f => f.Binding is { } binding
                        && ((CalculadoDeAtributo(binding) && DependenciasColetadas(f.Codigo)) || ProduzidoPelaClassificacao(binding)))
                    .Select(static f => f.Codigo)),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Os derivados do sistema que o processo resolve com os fatos coletados dados: os que têm todas
    /// as dependências declaradas (<see cref="DerivadosDoSistema"/>) entre eles.
    /// </summary>
    public static IEnumerable<string> DerivadosDoSistemaResolvidos(IReadOnlySet<string> coletados)
    {
        ArgumentNullException.ThrowIfNull(coletados);
        return DerivadosDoSistema.Dependencias.Where(d => d.Value.All(coletados.Contains)).Select(static d => d.Key);
    }

    /// <summary>Os fatos do catálogo cujos valores são as modalidades que o processo oferta.</summary>
    public static FatosDeModalidade ComValoresDeModalidade(IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return new FatosDeModalidade(catalogo.Where(static f => f.FonteValores == FonteModalidade).Select(static f => f.Codigo));
    }

    /// <summary>A fase canônica em que o catálogo situa cada fato, base da fase efetiva do fato no processo.</summary>
    public static Dictionary<string, string> PontoResolucaoPorFato(IEnumerable<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return catalogo.ToDictionary(static f => f.Codigo, static f => f.PontoResolucao, StringComparer.Ordinal);
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
    /// validada contra esse conjunto, nunca contra um catálogo global. O agregado sobre grupo
    /// repetível não tem opções próprias: as do processo são as do fato de membro.
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
                        processo.OpcoesDoProcesso(VinculoDeFato.Nomeado(fato.Binding, VinculoDeFato.AgregacaoDeGrupo) ?? fato.Codigo)
                            .Select(static o => o.Codigo));
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

    /// <summary>
    /// Os valores do catálogo que o processo congela para o fato: os ativos e os desativados que
    /// uma condição do processo já cita, porque desativar recusa só vínculo novo (ADR-0136). A
    /// mesma regra serve às opções do candidato e ao metadado do fato no edital.
    /// </summary>
    public static IReadOnlyList<FatoValorDominioViewItem> ValoresVigentes(
        FatoCandidatoView fato, IReadOnlySet<(string Fato, string Valor)> valoresCitados)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(valoresCitados);
        return [.. (fato.ValoresDominioDeclarados ?? []).Where(v => v.Ativo || valoresCitados.Contains((fato.Codigo, v.Codigo)))];
    }

    /// <summary>
    /// Os códigos que uma regra de derivação do fato pode contribuir no processo: os valores do
    /// domínio dinâmico enumerado, ou todos os valores do catálogo. Nulo quando a fonte não é
    /// enumerável pelo servidor. O valor desativado pertence ao domínio: quem o recusa como vínculo
    /// novo, com o motivo certo, é a conferência de vínculo novo.
    /// </summary>
    public static IReadOnlyCollection<string>? DominioDeContribuicao(
        FatoCandidatoView fato,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(dominiosDinamicos);

        if (dominiosDinamicos.TryGetValue(fato.Codigo, out DominioDeValores? dinamico))
        {
            return dinamico.Valores;
        }

        return fato.FonteValores == FonteGlobal ? [.. (fato.ValoresDominioDeclarados ?? []).Select(static v => v.Codigo)] : null;
    }
}

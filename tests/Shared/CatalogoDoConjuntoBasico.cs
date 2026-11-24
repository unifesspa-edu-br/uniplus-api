namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// O catálogo dos fatos do conjunto básico do candidato, como o seed o define, para os testes cujo
/// catálogo é montado à mão: o formulário de inscrição coleta esses fatos, e a escrita e a
/// publicação os conferem contra o catálogo.
/// </summary>
internal static class CatalogoDoConjuntoBasico
{
    /// <summary>O catálogo dado, completado com os fatos do conjunto básico que ele não traz.</summary>
    public static IReadOnlyList<FatoCandidatoView> Com(IEnumerable<FatoCandidatoView> catalogo)
    {
        List<FatoCandidatoView> lista = [.. catalogo];
        HashSet<string> presentes = new(lista.Select(static f => f.Codigo), StringComparer.Ordinal);
        return [.. lista, .. Fatos.Where(f => !presentes.Contains(f.Codigo))];
    }

    /// <summary>
    /// Os valores selecionáveis dados, completados com os que a publicação congela para os campos de
    /// seleção do conjunto básico: os do catálogo, e as UFs do Geo para os campos de UF.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ComValoresCongelados(
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valores = null)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> completos = new(valores ?? new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(), StringComparer.Ordinal);
        foreach (FatoCandidatoView fato in Fatos.Where(static f => f.DominioValor == TipoDoDado.Opcao && f.FonteValoresValor != FonteValoresFato.Municipios))
        {
            completos.TryAdd(fato.Codigo, fato.FonteValoresValor == FonteValoresFato.UnidadesFederativas
                ? [.. ReferenciaCidadeGeo.UnidadesFederativas.Select(static (uf, ordem) => new ValorDominioDeclaradoCongelado(uf.Sigla, uf.Nome, ordem))]
                : [.. fato.ValoresDominioDeclarados!.Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao, v.Ordem))]);
        }

        return completos;
    }

    /// <summary>Os fatos do conjunto básico.</summary>
    public static IReadOnlyList<FatoCandidatoView> Fatos { get; } =
    [
        Texto("NOME", "LIVRE"),
        Declarado("DESEJA_NOME_SOCIAL", TipoDoDado.SimOuNao),
        Texto("NOME_SOCIAL", "NOME_PESSOA", classificacao: "PUBLICO"),
        Categorico("NACIONALIDADE", "NATO", "NATURALIZADO", "ESTRANGEIRO"),
        Texto("CPF", "CPF", classificacao: "IDENTIFICADOR"),
        Texto("RG_NUMERO", "LIVRE", classificacao: "IDENTIFICADOR"),
        Categorico("RG_ORGAO_EMISSOR", "SSP", "PC", "DETRAN", "IFP", "PM", "CBM", "EB", "MB", "FAB", "DPF", "MRE", "OUTRO"),
        Texto("RG_ORGAO_EMISSOR_OUTRO", "LIVRE"),
        Geo("RG_UF", FonteValoresFato.UnidadesFederativas),
        Declarado("RG_DATA_EMISSAO", TipoDoDado.Data),
        Texto("PASSAPORTE_NUMERO", "LIVRE", classificacao: "IDENTIFICADOR"),
        Texto("RNM_NUMERO", "LIVRE", classificacao: "IDENTIFICADOR"),
        Declarado("DATA_NASCIMENTO", TipoDoDado.Data),
        Geo("NATURALIDADE_UF", FonteValoresFato.UnidadesFederativas),
        Geo("NATURALIDADE_MUNICIPIO", FonteValoresFato.Municipios),
        Texto("NOME_MAE", "NOME_PESSOA"),
        Texto("NOME_PAI", "NOME_PESSOA"),
        Categorico("ESTADO_CIVIL", "SOLTEIRO", "CASADO", "UNIAO_ESTAVEL", "SEPARADO", "DIVORCIADO", "VIUVO"),
        Categorico("SEXO", "FEMININO", "MASCULINO", "INTERSEXO"),
        Categorico("SEXO_CIVIL", "FEMININO", "MASCULINO"),
        Sensivel("IDENTIDADE_GENERO", "PESSOA_CIS", "PESSOA_TRANSGENERO", "TRAVESTI", "TRANSEXUAL", "PESSOA_NAO_BINARIA", "PREFIRO_NAO_INFORMAR"),
        Sensivel("ORIENTACAO_SEXUAL", "HETEROSSEXUAL", "HOMOSSEXUAL", "BISSEXUAL", "ASSEXUAL", "PANSEXUAL", "PREFIRO_NAO_INFORMAR"),
        Categorico("COR_RACA", "BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"),
        Texto("EMAIL", "EMAIL"),
        Texto("TELEFONE", "TELEFONE"),
        Categorico("TIPO_ENDERECO", "URBANO", "RURAL", "ALDEIA", "COMUNIDADE", "QUILOMBO", "VILA", "OUTRO"),
        Texto("NOME_COMUNIDADE", "LIVRE"),
        Declarado("ENDERECO_RESIDENCIAL", TipoDoDado.Endereco),
    ];

    private static FatoCandidatoView Declarado(
        string codigo, TipoDoDado dominio, string? formato = null, FonteValoresFato? fonte = null,
        IReadOnlyList<string>? valores = null, string classificacao = "PESSOAL") =>
        new(Guid.CreateVersion7(), codigo, codigo, "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(dominio), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), valores, "INSCRICAO", $"CAMPO_FORMULARIO:{codigo}",
            valores?.Select(static (v, ordem) => new FatoValorDominioViewItem(v, v, ordem, true)).ToList(), (fonte is { } fonteInformada ? CodigoComRotulo<FonteValoresFato>.De(fonteInformada) : null), Ativo: true,
            ClassificacaoProtecao: classificacao, HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Formato: formato, ProtecaoAprovada: true);

    private static FatoCandidatoView Texto(string codigo, string formato, string classificacao = "PESSOAL") =>
        Declarado(codigo, TipoDoDado.Texto, formato, classificacao: classificacao);

    private static FatoCandidatoView Geo(string codigo, FonteValoresFato fonte) => Declarado(codigo, TipoDoDado.Opcao, fonte: fonte);

    private static FatoCandidatoView Categorico(string codigo, params string[] valores) =>
        Declarado(codigo, TipoDoDado.Opcao, fonte: FonteValoresFato.Catalogo, valores: valores);

    private static FatoCandidatoView Sensivel(string codigo, params string[] valores) =>
        Declarado(codigo, TipoDoDado.Opcao, fonte: FonteValoresFato.Catalogo, valores: valores, classificacao: "SENSIVEL");
}

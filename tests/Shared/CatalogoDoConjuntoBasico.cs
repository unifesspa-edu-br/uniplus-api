namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
        foreach (FatoCandidatoView fato in Fatos.Where(static f => f.Dominio == "CATEGORICO" && f.FonteValores != "GEO_MUNICIPIO"))
        {
            completos.TryAdd(fato.Codigo, fato.FonteValores == "GEO_UF"
                ? [.. ReferenciaCidadeGeo.UnidadesFederativas.Select(static (uf, ordem) => new ValorDominioDeclaradoCongelado(uf.Sigla, uf.Nome, ordem))]
                : [.. fato.ValoresDominioDeclarados!.Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao, v.Ordem))]);
        }

        return completos;
    }

    /// <summary>Os fatos do conjunto básico.</summary>
    public static IReadOnlyList<FatoCandidatoView> Fatos { get; } =
    [
        Texto("NOME", "NOME_PESSOA"),
        Declarado("DESEJA_NOME_SOCIAL", "BOOLEANO"),
        Texto("NOME_SOCIAL", "NOME_PESSOA"),
        Categorico("NACIONALIDADE", "NATO", "NATURALIZADO", "ESTRANGEIRO"),
        Texto("CPF", "CPF"),
        Texto("RG_NUMERO", "LIVRE"),
        Texto("RG_ORGAO_EMISSOR", "LIVRE"),
        Geo("RG_UF", "GEO_UF"),
        Declarado("RG_DATA_EMISSAO", "DATA"),
        Categorico("DOCUMENTO_ESTRANGEIRO_TIPO", "PASSAPORTE", "RNM"),
        Texto("DOCUMENTO_ESTRANGEIRO_NUMERO", "LIVRE"),
        Declarado("DATA_NASCIMENTO", "DATA"),
        Geo("NATURALIDADE_UF", "GEO_UF"),
        Geo("NATURALIDADE_MUNICIPIO", "GEO_MUNICIPIO"),
        Texto("NOME_MAE", "NOME_PESSOA"),
        Texto("NOME_PAI", "NOME_PESSOA"),
        Categorico("ESTADO_CIVIL", "SOLTEIRO", "CASADO", "UNIAO_ESTAVEL", "SEPARADO", "DIVORCIADO", "VIUVO"),
        Categorico("SEXO", "FEMININO", "MASCULINO", "INTERSEXO"),
        Categorico("COR_RACA", "BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"),
        Texto("EMAIL", "EMAIL"),
        Texto("TELEFONE", "TELEFONE"),
        Declarado("ENDERECO_RESIDENCIAL", "ENDERECO"),
    ];

    private static FatoCandidatoView Declarado(string codigo, string dominio, string? formato = null, string? fonte = null, IReadOnlyList<string>? valores = null) =>
        new(Guid.CreateVersion7(), codigo, codigo, null, dominio, "DECLARADO", "ESCALAR", valores, "INSCRICAO", $"CAMPO_INSCRICAO:{codigo}",
            valores?.Select(static (v, ordem) => new FatoValorDominioViewItem(v, v, ordem, true)).ToList(), fonte, Ativo: true, Formato: formato);

    private static FatoCandidatoView Texto(string codigo, string formato) => Declarado(codigo, "TEXTO", formato);

    private static FatoCandidatoView Geo(string codigo, string fonte) => Declarado(codigo, "CATEGORICO", fonte: fonte);

    private static FatoCandidatoView Categorico(string codigo, params string[] valores) => Declarado(codigo, "CATEGORICO", fonte: "GLOBAL", valores: valores);
}

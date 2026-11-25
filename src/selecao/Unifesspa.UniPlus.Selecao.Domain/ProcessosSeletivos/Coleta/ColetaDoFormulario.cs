namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A visão da coleta de um formulário do processo (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0146): o formulário
/// declarado, os campos com o fato que cada um produz, os grupos repetíveis com os campos deles, as seções
/// e os termos, com os fatos que as regras de cada um citam, e o fato em que o pedido de isenção registra a
/// escolha de fundamentos, que fica fora da regra do campo produtor. É montada da configuração que congela na
/// publicação, e nunca do cadastro vivo.
/// </summary>
public sealed record ColetaDoFormulario(
    FormularioDeclarado Formulario,
    IReadOnlyList<ItemDoGrafo> Itens,
    IReadOnlyList<GrupoDoGrafo> Grupos,
    IReadOnlyCollection<PaginaDoGrafo> Secoes,
    IReadOnlyCollection<TermoDoGrafo> Termos,
    string? FatoDaEscolhaDeFundamentos = null)
{
    /// <summary>Os fatos dos campos de resposta única do formulário, sem os campos dos grupos repetíveis, que têm uma resposta por ocorrência.</summary>
    public IEnumerable<string> FatosDosItens => Itens.Select(static item => item.FatoCodigo);

    /// <summary>Os fatos que os campos do formulário produzem, inclusive os campos dos grupos repetíveis.</summary>
    public IEnumerable<string> FatosDosCampos =>
        Itens.Select(static item => item.FatoCodigo).Concat(Grupos.SelectMany(static grupo => grupo.Subitens).Select(static item => item.FatoCodigo));

    /// <summary>Os fatos que o formulário produz: os dos campos e o da escolha de fundamentos.</summary>
    public IEnumerable<string> FatosProduzidos => FatoDaEscolhaDeFundamentos is null ? FatosDosCampos : FatosDosCampos.Append(FatoDaEscolhaDeFundamentos);

    public bool Produz(string fato) => FatosProduzidos.Contains(fato, StringComparer.Ordinal);

    /// <summary>O código do grupo repetível de que o fato é campo, ou nulo quando o fato não é campo de grupo.</summary>
    public string? GrupoDoCampo(string fato) =>
        Grupos.FirstOrDefault(grupo => grupo.Subitens.Any(campo => string.Equals(campo.FatoCodigo, fato, StringComparison.Ordinal)))?.Codigo;

    /// <summary>
    /// As regras do formulário que citam fatos, cada uma com o caminho que a identifica: exibição e
    /// obrigatoriedade dos campos, dos grupos e dos campos dos grupos, exibição das seções e condição dos termos.
    /// </summary>
    public IEnumerable<RegraDoFormulario> Regras =>
    [
        .. Itens.Select(static item => new RegraDoFormulario($"itens.{item.FatoCodigo}", item.FatosCitados, null)),
        .. Grupos.Select(static grupo => new RegraDoFormulario($"grupos.{grupo.Codigo}", grupo.FatosCitados, null)),
        .. Grupos.SelectMany(static grupo => grupo.Subitens.Select(campo => new RegraDoFormulario($"grupos.{grupo.Codigo}.campos.{campo.FatoCodigo}", campo.FatosCitados, grupo.Codigo))),
        .. Secoes.Select(static secao => new RegraDoFormulario($"secoes.{secao.Codigo}", secao.FatosCitados, null)),
        .. Termos.Select(static termo => new RegraDoFormulario($"termos.{termo.Codigo}", termo.FatosCitados, null)),
    ];

    public bool ColetaGrupo(string codigoDoGrupo) => Grupos.Any(grupo => string.Equals(grupo.Codigo, codigoDoGrupo, StringComparison.Ordinal));
}

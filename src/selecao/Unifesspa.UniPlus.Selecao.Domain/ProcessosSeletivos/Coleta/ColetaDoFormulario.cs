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
    IReadOnlyCollection<EtapaDoGrafo> Secoes,
    IReadOnlyCollection<TermoDoGrafo> Termos,
    string? FatoDaEscolhaDeFundamentos = null)
{
    /// <summary>
    /// Os fatos que o formulário produz: o de cada campo, inclusive os campos dos grupos repetíveis, e o da
    /// escolha de fundamentos.
    /// </summary>
    public IEnumerable<string> FatosProduzidos =>
        Itens.Select(static item => item.FatoCodigo)
            .Concat(Grupos.SelectMany(static grupo => grupo.Subitens).Select(static item => item.FatoCodigo))
            .Concat(FatoDaEscolhaDeFundamentos is null ? [] : [FatoDaEscolhaDeFundamentos]);

    public bool Produz(string fato) => FatosProduzidos.Contains(fato, StringComparer.Ordinal);

    /// <summary>O código do grupo repetível de que o fato é campo, ou nulo quando o fato não é campo de grupo.</summary>
    public string? GrupoDoCampo(string fato) =>
        Grupos.FirstOrDefault(grupo => grupo.Subitens.Any(campo => string.Equals(campo.FatoCodigo, fato, StringComparison.Ordinal)))?.Codigo;

    public bool ColetaGrupo(string codigoDoGrupo) => Grupos.Any(grupo => string.Equals(grupo.Codigo, codigoDoGrupo, StringComparison.Ordinal));
}

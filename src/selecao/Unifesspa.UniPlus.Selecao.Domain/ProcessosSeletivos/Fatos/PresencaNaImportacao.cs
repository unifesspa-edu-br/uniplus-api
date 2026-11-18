namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// O fato derivado "veio na importação" de um dado básico (UNI-REQ-0151) e os fatos que formam o dado, na
/// granularidade em que ele vale: o RG vale com número, órgão, UF e data. O fato de presença não depende de
/// outro fato, e é por isso que o campo que complementa o dado pode citá-lo sem formar ciclo com ele.
/// </summary>
public sealed record PresencaNaImportacao(string Fato, IReadOnlySet<string> FatosDoDadoBasico);

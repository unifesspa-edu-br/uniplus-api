namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

/// <summary>
/// Uma regra de um formulário que cita fatos: o caminho que a identifica no formulário, os fatos que cita e o
/// grupo repetível a que pertence, quando é regra de campo de grupo (UNI-REQ-0145, UNI-REQ-0146).
/// </summary>
public sealed record RegraDoFormulario(string Caminho, IReadOnlyCollection<string> FatosCitados, string? GrupoProprio);

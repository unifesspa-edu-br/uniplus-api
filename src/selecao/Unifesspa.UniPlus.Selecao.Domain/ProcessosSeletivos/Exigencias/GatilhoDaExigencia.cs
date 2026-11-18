namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O gatilho de uma exigência de documento visto pela conferência: o formulário que entrega a exigência, a
/// fase em que ela é exigida, os fatos que o gatilho cita e o grupo repetível por que a exigência repete, quando
/// repete: só nesse caso o gatilho cita os campos do grupo (UNI-REQ-0064, UNI-REQ-0144).
/// </summary>
public sealed record GatilhoDaExigencia(CodigoDoFormulario CodigoDoFormulario, Guid FaseId, IReadOnlyCollection<string> FatosCitados, string? GrupoDeRepeticao = null);

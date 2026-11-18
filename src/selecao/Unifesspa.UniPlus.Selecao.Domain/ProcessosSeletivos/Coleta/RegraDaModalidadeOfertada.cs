namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

/// <summary>
/// A regra de aplicabilidade de uma modalidade ofertada no processo, no que a conferência olha: o código da
/// modalidade e os fatos que a regra cita (UNI-REQ-0076, UNI-REQ-0075).
/// </summary>
public sealed record RegraDaModalidadeOfertada(string CodigoDaModalidade, IReadOnlyCollection<string> FatosCitados);

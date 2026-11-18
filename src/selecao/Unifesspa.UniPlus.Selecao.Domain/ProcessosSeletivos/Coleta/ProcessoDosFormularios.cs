namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

/// <summary>
/// O que o processo diz de si às regras dos formulários (UNI-REQ-0144): se a inscrição é pelo sistema ou o
/// resultado é importado, se cobra taxa e, no resultado importado, a primeira fase depois da importação.
/// </summary>
public sealed record ProcessoDosFormularios(bool InscricaoPeloSistema, bool CobraTaxa, Guid? PrimeiraFaseDepoisDaImportacaoId);

namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O documento exigido uma vez por opção que o processo oferece para o fato, salvo as de
/// <paramref name="Exceto"/>. As opções só existem no processo: o modelo não as enumera, e a opção
/// que o processo passar a oferecer entra na próxima aplicação sem mudar o modelo.
/// </summary>
public sealed record ExigenciaPorOpcao(string FatoCodigo, IReadOnlyList<string> Exceto);

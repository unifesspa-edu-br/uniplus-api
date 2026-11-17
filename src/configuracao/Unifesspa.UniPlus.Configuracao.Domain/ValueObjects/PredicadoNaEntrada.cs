namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Uma regra sobre fatos de uma parte do conteúdo do modelo e o caminho do campo que a carrega na
/// entrada, relativo à parte. O predicado é ausente quando a parte não declara a regra.
/// </summary>
public sealed record PredicadoNaEntrada(string Caminho, PredicadoDnf? Predicado);

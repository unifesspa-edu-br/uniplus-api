namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

/// <summary>
/// Marca o filtro de uma rota que exige mais do que estar autenticado — permissão, papel ou
/// vínculo com o recurso —, de modo que a rota possa recusar quem se identificou. O contrato
/// OpenAPI declara o <c>403</c> só nas rotas que carregam esta marca, ou um
/// <c>[Authorize]</c> com papel ou política.
/// </summary>
public interface IExigeAutorizacaoAlemDoLogin;

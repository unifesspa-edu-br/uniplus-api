namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Pergunta de uma linha para quem atende a requisição: o solicitante pode
/// executar esta operação?
/// </summary>
/// <remarks>
/// O ponto de decisão (<see cref="IAuthorizationDecisionService"/>) recebe o
/// sujeito, o recurso e o contexto já montados, porque é assim que a decisão
/// fica reproduzível e testável. Montar essas três coisas em cada endpoint,
/// porém, é cerimônia que não pertence a quem só precisa saber se pode seguir —
/// esta porta faz esse trabalho e devolve a resposta.
/// </para>
/// <para>
/// O desfecho não é um booleano porque a borda tem três respostas, não duas:
/// colapsar identidade incompleta em "não pode" faria o endpoint responder
/// <c>403</c> a quem sequer pôde ser identificado.
/// </remarks>
public interface IVerificadorDeAcesso
{
    /// <summary>
    /// Verifica se o solicitante da requisição corrente pode executar a operação
    /// que a permissão protege.
    /// </summary>
    /// <returns>
    /// <see cref="ResultadoDoAcesso.Permitido"/>, <see cref="ResultadoDoAcesso.Negado"/>
    /// ou <see cref="ResultadoDoAcesso.IdentidadeIncompleta"/> — os três desfechos
    /// que a borda responde de forma distinta.
    /// </returns>
    /// <param name="permissao">Permissão exigida, do catálogo.</param>
    /// <param name="recurso">
    /// Recurso alvo, quando a decisão depende de escopo (unidade, processo,
    /// chamada). Omitido, a decisão considera apenas a permissão.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<ResultadoDoAcesso> VerificarAsync(
        PermissionRequirement permissao,
        ResourceContext? recurso = null,
        CancellationToken cancellationToken = default);
}

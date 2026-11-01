namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Pergunta de uma linha para quem atende a requisição: o solicitante pode
/// executar esta operação?
/// </summary>
/// <remarks>
/// O ponto de decisão (<see cref="IAuthorizationDecisionService"/>) recebe o
/// sujeito, o resource e o contexto já montados, porque é assim que a decisão
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
    /// <param name="resource">
    /// Resource alvo, quando a decisão depende de escopo (unidade, processo,
    /// chamada). Omitido, a decisão considera apenas a permissão.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<ResultadoDoAcesso> VerificarAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o solicitante tem a permissão em ao menos uma unidade, para a operação cujo
    /// alvo não pertence a unidade alguma. A decisão é a do ponto único, tomada uma vez por
    /// unidade em que o solicitante tem concessão vigente da permissão.
    /// </summary>
    /// <param name="permissao">Permissão que alguma concessão de unidade deve cobrir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<ResultadoDoAcesso> VerificarEmAlgumaUnidadeAsync(
        PermissionRequirement permissao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta de capacidade: responde se o solicitante poderia executar a operação sobre o resource, com as mesmas
    /// verificações de contexto da decisão (unidade dona, impedimento por inscrição e as demais), sem gravar nada. A
    /// resposta serve a mostrar o que a pessoa pode fazer, e <b>não vale como decisão</b> para entregar dado: quem
    /// entrega o dado decide por <see cref="VerificarAsync"/>, ou decide por
    /// <see cref="DecidirSemRegistrarAsync"/> e registra a permitida por <see cref="RegistrarAsync(DecisaoDeAcesso, CancellationToken)"/>.
    /// </summary>
    Task<ResultadoDoAcesso> PodeAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decide como <see cref="VerificarAsync"/>, sem gravar nada, nem a negativa, e devolve a decisão para que o
    /// chamador registre a permitida. É o caminho do projetor, em que a negativa não é tentativa de ninguém.
    /// </summary>
    Task<DecisaoDeAcesso> DecidirSemRegistrarAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra de forma durável uma decisão permitida sobre o resource em que foi tomada. A falha de gravação lança
    /// <see cref="RegistroDeAcessoNaoGravadoException"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A decisão não foi permitida: a negativa não se registra por aqui.</exception>
    Task RegistrarAsync(DecisaoDeAcesso permitida, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra uma decisão permitida para vários resources: um registro por id, cada um com a permissão e a
    /// sensibilidade da decisão de origem, num lote cuja nova tentativa repete as mesmas chaves. É
    /// como a listagem registra uma inscrição por linha tendo decidido uma vez só, sobre o processo.
    /// </summary>
    /// <param name="permitida">A decisão permitida de origem.</param>
    /// <param name="resourceTipo">O tipo do resource de cada registro.</param>
    /// <param name="resourceIds">Os ids dos resources, um registro por id.</param>
    /// <param name="resourceEhInscricao">Se o resource é a inscrição, para o registro nomeá-la.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="InvalidOperationException">A decisão não foi permitida.</exception>
    Task RegistrarParaCadaAsync(
        DecisaoDeAcesso permitida,
        string resourceTipo,
        IReadOnlyCollection<Guid> resourceIds,
        bool resourceEhInscricao = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra, numa só gravação, várias decisões tomadas sem registro, permitidas ou negadas. É para quem verifica
    /// mais de uma permissão numa pergunta e quer gravar só o que vale: as permitidas que habilitam a entrega ou, se
    /// nenhuma habilitou e a requisição é recusada, as negativas, que então são a tentativa real. Decisões sem registro
    /// (identidade incompleta) são ignoradas.
    /// </summary>
    Task RegistrarAsync(IReadOnlyCollection<DecisaoDeAcesso> decisoes, CancellationToken cancellationToken = default);
}

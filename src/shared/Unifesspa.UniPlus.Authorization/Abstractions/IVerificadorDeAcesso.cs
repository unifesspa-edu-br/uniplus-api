namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Pergunta de uma linha para quem atende a requisição: o solicitante pode executar esta operação?
/// </summary>
/// <remarks>
/// <para>
/// O ponto de decisão (<see cref="IAuthorizationDecisionService"/>) recebe o sujeito, o resource e o contexto já
/// montados, porque é assim que a decisão fica reproduzível e testável. Montar essas três coisas em cada endpoint,
/// porém, é cerimônia que não pertence a quem só precisa saber se pode seguir — esta porta faz esse trabalho e devolve
/// a resposta.
/// </para>
/// <para>
/// Cada método nomeia a intenção de quem pergunta, e é ela que decide se há registro: o pedido que pode ser recusado
/// grava, a capacidade e a avaliação não gravam. O desfecho não é um booleano porque a borda tem três respostas, não
/// duas: colapsar identidade incompleta em "não pode" faria o endpoint responder <c>403</c> a quem sequer pôde ser
/// identificado.
/// </para>
/// </remarks>
public interface IVerificadorDeAcesso
{
    /// <summary>
    /// Pedido que pode ser recusado: decide e registra pela política de registro, com a intenção de pedido.
    /// </summary>
    /// <returns>
    /// <see cref="ResultadoDoAcesso.Permitido"/>, <see cref="ResultadoDoAcesso.Negado"/>,
    /// <see cref="ResultadoDoAcesso.Impedido"/> ou <see cref="ResultadoDoAcesso.IdentidadeIncompleta"/>.
    /// </returns>
    /// <param name="requisito">Permissão exigida, do catálogo.</param>
    /// <param name="resource">
    /// Resource alvo, quando a decisão depende de escopo (unidade, processo, chamada). Omitido, a decisão considera
    /// apenas a permissão.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="RegistroDeAcessoNaoGravadoException">O registro exigido não foi gravado.</exception>
    Task<ResultadoDoAcesso> ExigirAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capacidade: responde se o solicitante poderia executar a operação sobre o resource, com as mesmas verificações de
    /// contexto da decisão (unidade dona, impedimento por inscrição e as demais), sem gravar nada. Serve a mostrar o que
    /// a pessoa pode fazer, e <b>não vale como decisão</b> para entregar dado: quem entrega o dado decide por
    /// <see cref="ExigirAsync"/>, ou avalia por <see cref="AvaliarAsync"/> e registra o desfecho.
    /// </summary>
    Task<ResultadoDoAcesso> PodeAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Avalia como <see cref="ExigirAsync"/>, sem gravar nada, e devolve a decisão para que o chamador a componha com
    /// outras e registre o desfecho. É o caminho do projetor, do aprovador, da capacidade que pode recusar e do principal
    /// do filtro de permissão.
    /// </summary>
    Task<DecisaoDeAcesso> AvaliarAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Avalia, sem gravar, a alternativa "em alguma unidade": a permissão vale se o solicitante a tem concedida em ao
    /// menos uma unidade. Devolve uma decisão por unidade com concessão, na ordem das concessões, até a primeira
    /// permitida. Sem concessão em unidade, devolve só a decisão sem unidade, que é sempre negada: a concessão global
    /// não autoriza esta alternativa. Sem sujeito, devolve só <see cref="SemSujeito"/>.
    /// </summary>
    /// <param name="requisito">Permissão que alguma concessão de unidade deve cobrir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<DecisaoDeAcesso>> AvaliarEmAlgumaUnidadeAsync(
        PermissionRequirement requisito,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava o desfecho de decisões avaliadas pela política de registro: a permitida que autorizou ou, se nenhuma
    /// autorizou, as negadas. Decisão sem sujeito é ignorada. A falha de gravação lança
    /// <see cref="RegistroDeAcessoNaoGravadoException"/>.
    /// </summary>
    /// <param name="decisoes">As decisões avaliadas por uma mesma pergunta.</param>
    /// <param name="intencao">Para que o chamador decidiu; define o que se grava das permitidas.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task RegistrarDesfechoAsync(
        IReadOnlyCollection<DecisaoDeAcesso> decisoes,
        IntencaoDoRegistro intencao,
        CancellationToken cancellationToken = default);

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
    Task RegistrarParaCadaAsync(
        DecisaoPermitida permitida,
        string resourceTipo,
        IReadOnlyCollection<Guid> resourceIds,
        bool resourceEhInscricao = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra a leitura que a parte faz do que é seu, sem concessão, como a do próprio pedido de concessão (ADR-0166):
    /// um registro permitido por resource exibido, com a fonte <see cref="Enums.FonteGrant.Titularidade"/>, o código da
    /// operação do titular e os campos da concessão nulos, no mesmo lote atômico e com a mesma recusa de
    /// <see cref="RegistrarParaCadaAsync"/>. Não decide nada: quem chama já conferiu que a pessoa é parte, e a
    /// titularidade nunca autoriza no ponto de decisão. Sem resource exibido, nada se grava.
    /// </summary>
    /// <param name="operacaoDoTitular">
    /// O código fixo da operação, fora do catálogo de permissões e não concedível, e a sensibilidade do dado lido.
    /// </param>
    /// <param name="resourceTipo">O tipo do resource de cada registro.</param>
    /// <param name="resourceIds">Os ids dos resources exibidos, um registro por id.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="ResultadoDoAcesso.Permitido"/> com a leitura registrada, ou
    /// <see cref="ResultadoDoAcesso.IdentidadeIncompleta"/> quando o token não identifica a pessoa, sem gravar nada.
    /// </returns>
    /// <exception cref="RegistroDeAcessoNaoGravadoException">O registro exigido não foi gravado.</exception>
    Task<ResultadoDoAcesso> RegistrarLeituraPorTitularidadeAsync(
        PermissionRequirement operacaoDoTitular,
        string resourceTipo,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken = default);
}

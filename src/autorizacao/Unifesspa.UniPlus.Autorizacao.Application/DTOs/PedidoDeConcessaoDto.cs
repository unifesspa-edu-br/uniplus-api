namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>
/// Quem recebe o acesso, como o aprovador precisa conhecê-lo: o e-mail institucional do pedido e, depois do aceite, a
/// identidade e o nome do token dele. Nunca traz CPF.
/// </summary>
/// <param name="Identidade">Nula enquanto o beneficiário não aceitou o pedido.</param>
/// <param name="Nome">Nome do token do beneficiário, gravado no aceite; nulo antes dele.</param>
public sealed record BeneficiarioDoPedidoDto(string Email, IdentidadeDto? Identidade, string? Nome);

/// <summary>Decisão de um aprovador, com o retrato do perfil que ele avaliou.</summary>
/// <param name="Dispensa">Motivo da dispensa da segunda aprovação, quando a decisão a dispensou.</param>
public sealed record DecisaoDoPedidoDto(
    string Etapa,
    IdentidadeDto Decisor,
    string Resultado,
    string? Motivo,
    DateTimeOffset Instante,
    IReadOnlyList<string> PermissoesAvaliadas,
    string SensibilidadeAvaliada,
    string? Dispensa);

public sealed record AnexoDoPedidoDto(Guid Id, string Situacao, long TamanhoDeclarado, DateTimeOffset? EnviadoEm);

/// <summary>Pedido de concessão como a API o expõe a quem pode vê-lo.</summary>
/// <param name="EtapaAtual">Etapa que o pedido aguarda do aprovador (<c>gestor</c> ou <c>protecao_de_dados</c>); nula se não aguarda nenhum.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugeriu; nulo, sem sugestão.</param>
/// <param name="ValidaAte">Fim de vigência que o gestor definiu ao aprovar; nulo, sem prazo.</param>
/// <param name="ConcessaoId">A concessão que a última aprovação gerou; nula até lá.</param>
public sealed record PedidoDeConcessaoDto(
    Guid Id,
    string Situacao,
    string? EtapaAtual,
    IdentidadeDto Solicitante,
    BeneficiarioDoPedidoDto Beneficiario,
    PerfilResumoDto Perfil,
    EscopoDto Escopo,
    string Justificativa,
    DateTimeOffset? ValidaAteSugerida,
    DateTimeOffset? ValidaAte,
    DateTimeOffset CriadoEm,
    DateTimeOffset? EncerradoEm,
    Guid? ConcessaoId,
    IReadOnlyList<AnexoDoPedidoDto> Anexos,
    IReadOnlyList<DecisaoDoPedidoDto> Decisoes);

/// <summary>Pedido como aparece nas listas e nas filas dos aprovadores.</summary>
public sealed record PedidoResumoDto(
    Guid Id,
    string Situacao,
    string? EtapaAtual,
    BeneficiarioDoPedidoDto Beneficiario,
    PerfilResumoDto Perfil,
    EscopoDto Escopo,
    string Justificativa,
    DateTimeOffset CriadoEm);

/// <summary>Resposta do envio de um anexo: o id e a URL de PUT assinada para o PDF.</summary>
/// <param name="UrlDeEnvio">URL de PUT assinada; credencial de acesso, de validade curta.</param>
/// <param name="TipoDeMidia">O <c>Content-Type</c> que o PUT precisa enviar.</param>
public sealed record AnexoParaEnvioDto(Guid Id, string Situacao, Uri UrlDeEnvio, string TipoDeMidia, DateTimeOffset ExpiraEm);

/// <summary>URL assinada de leitura de um anexo verificado, de validade curta.</summary>
public sealed record LeituraDoAnexoDto(Uri Url, DateTimeOffset ExpiraEm);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao</c>.</summary>
/// <param name="BeneficiarioEmail">E-mail institucional do beneficiário; em branco, o pedido é para o próprio solicitante.</param>
/// <param name="UnidadeId">Unidade do escopo; ausente só para o perfil de governança, de escopo institucional.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugere ao gestor; opcional e não vale para o perfil de governança.</param>
public sealed record CriarPedidoDeConcessaoRequest(
    string? BeneficiarioEmail, Guid PerfilId, Guid? UnidadeId, string? Justificativa, DateTimeOffset? ValidaAteSugerida = null);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/anexos</c>.</summary>
public sealed record EnviarAnexoDoPedidoRequest(long TamanhoDoPdf);

/// <summary>Corpo da confirmação do anexo.</summary>
public sealed record ConfirmarAnexoDoPedidoRequest(string? Etag);

/// <summary>Corpo opcional do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/aprovacoes</c>.</summary>
/// <param name="ValidaAte">Fim da vigência da concessão, definido pelo gestor; só vale na etapa dele. Ausente, sem prazo.</param>
public sealed record AprovarPedidoDeConcessaoRequest(DateTimeOffset? ValidaAte = null);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/recusa</c>.</summary>
public sealed record RecusarPedidoDeConcessaoRequest(string? Motivo);

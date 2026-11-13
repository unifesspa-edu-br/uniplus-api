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

/// <summary>
/// Registro de que uma versão nova do perfil entrou em vigor enquanto o pedido estava ativo, e do que isso fez a ele. Os
/// números são os das versões; o pedido não guarda cópia do perfil.
/// </summary>
/// <param name="Desfecho"><c>versao_atualizada</c>, <c>voltou_ao_gestor</c> ou <c>devolvido</c>.</param>
public sealed record TrocaDeVersaoDoPedidoDto(int VersaoAnterior, int VersaoNova, string Desfecho, DateTimeOffset Instante);

/// <summary>Pedido de concessão como a API o expõe a quem pode vê-lo.</summary>
/// <param name="Versao">Número da versão do perfil que o pedido segue; nulo no perfil de governança, que não tem versão.</param>
/// <param name="EtapaAtual">Etapa que o pedido aguarda do aprovador (<c>gestor</c> ou <c>protecao_de_dados</c>); nula se não aguarda nenhum.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugeriu; nulo, sem sugestão.</param>
/// <param name="ValidaAte">Fim de vigência que o gestor definiu ao aprovar; nulo, sem prazo.</param>
/// <param name="AdmitePrazo">Se a concessão pode ter prazo; falso só para o perfil de gestor de acessos.</param>
/// <param name="ConcessaoId">A concessão que a última aprovação gerou; nula até lá.</param>
/// <param name="AnexaraDocumento">O solicitante declarou, na criação, que anexaria documento; sempre verdadeiro no perfil de governança.</param>
/// <param name="Termo">O termo de responsabilidade fixado no pedido protegido e o aceite dele; nulo no pedido sem termo.</param>
public sealed record PedidoDeConcessaoDto(
    Guid Id,
    string Situacao,
    string? EtapaAtual,
    IdentidadeDto Solicitante,
    BeneficiarioDoPedidoDto Beneficiario,
    PerfilResumoDto Perfil,
    int? Versao,
    EscopoDto Escopo,
    string Justificativa,
    DateTimeOffset? ValidaAteSugerida,
    DateTimeOffset? ValidaAte,
    bool AdmitePrazo,
    DateTimeOffset CriadoEm,
    DateTimeOffset? EncerradoEm,
    Guid? ConcessaoId,
    IReadOnlyList<AnexoDoPedidoDto> Anexos,
    IReadOnlyList<DecisaoDoPedidoDto> Decisoes,
    IReadOnlyList<TrocaDeVersaoDoPedidoDto> TrocasDeVersao,
    bool AnexaraDocumento,
    TermoDoPedidoDto? Termo);

/// <summary>
/// A versão do termo de responsabilidade fixada no pedido. O texto não vem aqui: a tela o lê pela versão, em
/// <c>GET /api/autorizacao/termos-de-responsabilidade/versoes/{versaoId}</c>.
/// </summary>
/// <param name="Aceite">O aceite do beneficiário; nulo até ele aceitar.</param>
public sealed record TermoDoPedidoDto(Guid VersaoId, int Numero, string Resumo, AceiteDoTermoDto? Aceite);

/// <summary>A evidência do aceite do termo: quem aceitou, quando, e a versão e o resumo do texto apresentado.</summary>
public sealed record AceiteDoTermoDto(IdentidadeDto Pessoa, DateTimeOffset AceitoEm, Guid VersaoId, int Numero, string Resumo);

/// <summary>Pedido como aparece nas listas e nas filas dos aprovadores.</summary>
/// <param name="AguardaAceiteDoTermo">O pedido aguarda, do beneficiário, o aceite do termo de responsabilidade fixado.</param>
public sealed record PedidoResumoDto(
    Guid Id,
    string Situacao,
    string? EtapaAtual,
    BeneficiarioDoPedidoDto Beneficiario,
    PerfilResumoDto Perfil,
    int? Versao,
    EscopoDto Escopo,
    string Justificativa,
    DateTimeOffset CriadoEm,
    bool AguardaAceiteDoTermo);

/// <summary>Resposta do envio de um anexo: o id e a URL de PUT assinada para o PDF.</summary>
/// <param name="UrlDeEnvio">URL de PUT assinada; credencial de acesso, de validade curta.</param>
/// <param name="TipoDeMidia">O <c>Content-Type</c> que o PUT precisa enviar.</param>
public sealed record AnexoParaEnvioDto(Guid Id, string Situacao, Uri UrlDeEnvio, string TipoDeMidia, DateTimeOffset ExpiraEm);

/// <summary>URL assinada de leitura de um anexo verificado, de validade curta.</summary>
public sealed record LeituraDoAnexoDto(Uri Url, DateTimeOffset ExpiraEm);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao</c>.</summary>
/// <param name="BeneficiarioEmail">E-mail institucional do beneficiário; em branco, o pedido é para o próprio solicitante.</param>
/// <param name="UnidadeId">Unidade do escopo; ausente só para o perfil de governança, de escopo institucional.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugere ao gestor; opcional e não vale para o perfil de gestor de acessos.</param>
/// <param name="AnexaraDocumento">
/// O solicitante vai anexar documento: o pedido espera a verificação dele antes do aceite e do gestor. No pedido protegido,
/// só o documento declarado aqui se anexa; o perfil de governança o exige sempre.
/// </param>
/// <param name="TermoVersaoApresentadaId">
/// A versão do termo de responsabilidade que a tela apresentou. Obrigatória em todo pedido protegido e conferida com a
/// vigente (UNI-REQ-0377); no pedido para si mesmo sem documento, o envio é também o aceite. Proibida no pedido sem termo.
/// Divergente da vigente, a resposta é conflito e a reapresentação usa chave de idempotência nova.
/// </param>
public sealed record CriarPedidoDeConcessaoRequest(
    string? BeneficiarioEmail,
    Guid PerfilId,
    Guid? UnidadeId,
    string? Justificativa,
    DateTimeOffset? ValidaAteSugerida = null,
    bool AnexaraDocumento = false,
    Guid? TermoVersaoApresentadaId = null);

/// <summary>Corpo opcional do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/aceite</c>.</summary>
/// <param name="TermoVersaoApresentadaId">
/// A versão do termo fixada no pedido, que a tela apresentou ao beneficiário; obrigatória no pedido com termo e proibida
/// nos demais.
/// </param>
public sealed record AceitarPedidoDeConcessaoRequest(Guid? TermoVersaoApresentadaId = null);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/anexos</c>.</summary>
public sealed record EnviarAnexoDoPedidoRequest(long TamanhoDoPdf);

/// <summary>Corpo da confirmação do anexo.</summary>
public sealed record ConfirmarAnexoDoPedidoRequest(string? Etag);

/// <summary>Corpo opcional do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/aprovacoes</c>.</summary>
/// <param name="ValidaAte">Fim da vigência da concessão, definido pelo gestor; só vale na etapa dele. Ausente, sem prazo.</param>
public sealed record AprovarPedidoDeConcessaoRequest(DateTimeOffset? ValidaAte = null);

/// <summary>Corpo do <c>POST /api/autorizacao/pedidos-de-concessao/{id}/recusa</c>.</summary>
public sealed record RecusarPedidoDeConcessaoRequest(string? Motivo);

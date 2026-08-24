namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resposta do passo 1 (iniciar upload) — Story #759, T3 #784.
/// <paramref name="ContentTypeExigido"/> é o valor exato do header
/// <c>Content-Type</c> que o PUT direto ao <see cref="UrlUpload"/> precisa
/// enviar — a assinatura SigV4 da URL o inclui como header assinado, então
/// qualquer variação (ex.: <c>application/pdf; charset=utf-8</c>) faz o
/// MinIO rejeitar com SignatureDoesNotMatch antes de chegar à validação de
/// negócio.
/// </summary>
public sealed record IniciarUploadDocumentoEditalDto(
    Guid DocumentoEditalId,
    Uri UrlUpload,
    string ContentTypeExigido,
    DateTimeOffset ExpiraEm);

/// <summary>
/// Documento do Edital como o cliente administrativo o enxerga — resposta do
/// passo 3 (confirmar upload) e item da leitura dos documentos de um Processo
/// Seletivo. Um só tipo para os dois estados do ciclo de vida: o pendente
/// existe de verdade no contrato, porque é ele que deixa a retomada de um
/// rascunho distinguir "o upload nunca foi confirmado" de "não há documento".
/// <para>
/// Nada aqui endereça o storage. <c>ObjectKey</c>, <c>ObjectKeyConfirmado</c> e
/// as URLs pre-assinadas ficam de fora por decisão de contrato, não por
/// esquecimento: a URL de PUT ainda vale até o TTL expirar, e quem a conhece
/// pode sobrescrever o objeto que o documento pendente aponta.
/// </para>
/// </summary>
/// <param name="CriadoEm">Instante em que o upload foi iniciado.</param>
/// <param name="ExpiraEm">
/// Fim da validade da URL pre-assinada emitida no início do upload. Continua
/// preenchido depois da confirmação como registro do prazo que valeu — não é
/// prazo de validade do documento confirmado, que é imutável e não expira.
/// </param>
/// <param name="TamanhoBytes">Nulo enquanto pendente — o tamanho só é conhecido ao ler o objeto na confirmação.</param>
/// <param name="HashSha256">Nulo enquanto pendente — calculado server-side na confirmação.</param>
/// <param name="ConfirmadoEm">Nulo enquanto pendente.</param>
public sealed record DocumentoEditalDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    string Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset ExpiraEm,
    long? TamanhoBytes,
    string? HashSha256,
    DateTimeOffset? ConfirmadoEm);

namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Instalação como a API a expõe a quem a iniciou. Nunca traz o código nem o resumo dele.</summary>
/// <param name="Situacao">Código textual da situação, ex.: <c>aguardando_codigo</c>.</param>
/// <param name="CodigoExpiraEm">Fim do prazo do código; nulo fora da espera do código (antes do envio e em estado terminal).</param>
/// <param name="TentativasRestantes">Códigos errados que ainda se admitem; nulo fora da espera do código (antes do envio e em estado terminal).</param>
public sealed record InstalacaoDto(
    Guid Id,
    string Situacao,
    string NumeroDoDocumento,
    IReadOnlyList<IdentidadeDto> Gestores,
    DateTimeOffset IniciadaEm,
    DateTimeOffset? CodigoExpiraEm,
    int? TentativasRestantes);

/// <summary>Resposta do início: o id e a URL de PUT assinada para o PDF.</summary>
/// <param name="UrlDeEnvio">URL de PUT assinada; credencial de acesso, de validade curta.</param>
/// <param name="TipoDeMidia">O <c>Content-Type</c> que o PUT precisa enviar.</param>
/// <param name="ExpiraEm">Fim da validade da URL.</param>
public sealed record InstalacaoIniciadaDto(
    Guid Id,
    string Situacao,
    Uri UrlDeEnvio,
    string TipoDeMidia,
    DateTimeOffset ExpiraEm);

/// <summary>Tentativa de confirmar a instalação, para a auditoria. Nunca traz o código.</summary>
public sealed record TentativaDeInstalacaoDto(
    Guid Id,
    Guid? InstalacaoId,
    IdentidadeDto Autor,
    DateTimeOffset Instante,
    string Resultado);

/// <summary>
/// Gestor como a requisição o informou; campo ausente volta como recusa de gestores inválidos.
/// </summary>
/// <param name="Email">E-mail institucional do gestor, obrigatório: a revogação da concessão o pede de volta.</param>
/// <param name="Nome">Nome de exibição do gestor, opcional; a trilha o mostra.</param>
public sealed record IdentidadeInformadaDto(string? Emissor, string? Sub, string? Email = null, string? Nome = null);

/// <summary>Corpo do <c>POST /api/autorizacao/instalacao</c>.</summary>
public sealed record IniciarInstalacaoRequest(
    IReadOnlyList<IdentidadeInformadaDto?>? Gestores,
    string? NumeroDoDocumento,
    long TamanhoDoPdf);

/// <summary>Corpo do <c>POST /api/autorizacao/instalacao/{id}/documento/confirmacao</c>.</summary>
public sealed record ConfirmarDocumentoDaInstalacaoRequest(string? Etag);

/// <summary>Corpo do <c>POST /api/autorizacao/instalacao/confirmacao</c>.</summary>
public sealed record ConfirmarInstalacaoRequest(string? Codigo);

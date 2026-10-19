namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

/// <summary>
/// Corpo de <see cref="Controllers.AtosDoProcessoController.Publicar"/> — omite o processo, que vem da
/// rota. O órgão, a série e o ano do ato não são declarados: saem do processo, do tipo e da data.
/// </summary>
/// <param name="TipoAtoCodigo">Código do tipo do ato no catálogo; não pode ser resultado nem congelar configuração.</param>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataPublicacao">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="DocumentoPrincipalId">O documento a publicar confirmado que é o principal, sempre PDF.</param>
/// <param name="Anexos">Os anexos, na ordem em que o ato os apresenta, até 20. Ausente ou vazio quando não há.</param>
/// <param name="AtoCorrigidoId">O ato publicado que este corrige, quando é errata ou retificação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção; obrigatório junto com <paramref name="AtoCorrigidoId"/>.</param>
public sealed record PublicarAtoAvulsoDoProcessoRequest(
    string TipoAtoCodigo,
    string? Numero,
    DateOnly DataPublicacao,
    string Assinante,
    Guid DocumentoPrincipalId,
    IReadOnlyList<AnexoDoAtoRequest>? Anexos = null,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null);

/// <summary>
/// Um anexo do ato: o documento a publicar já confirmado e o nome com que o arquivo é baixado.
/// </summary>
/// <param name="DocumentoId">O documento a publicar confirmado, do mesmo processo.</param>
/// <param name="Nome">
/// Nome do arquivo no download. A extensão do formato é acrescentada quando falta; o nome é
/// obrigatório, tem até 120 caracteres com a extensão e não admite separador de caminho.
/// </param>
public sealed record AnexoDoAtoRequest(Guid DocumentoId, string? Nome)
{
    /// <summary>Os anexos do corpo como o comando os recebe; um item nulo segue adiante para o validator recusar.</summary>
    internal static List<AnexoSolicitadoDoAto>? ParaComando(IReadOnlyList<AnexoDoAtoRequest>? anexos) =>
        anexos?.Select(static a => a is null ? null! : new AnexoSolicitadoDoAto(a.DocumentoId, a.Nome)).ToList();
}

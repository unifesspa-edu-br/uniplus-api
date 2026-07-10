namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Registra um ato publicado. O tipo é resolvido no catálogo vigente na
/// <paramref name="DataPublicacao"/> e seus atributos de consequência
/// (<c>congela_configuracao</c>, <c>efeito_irreversivel</c>) são copiados por
/// valor server-side — o cliente não os informa, então não há como divergirem
/// do catálogo (AC5).
/// </summary>
/// <remarks>
/// <para>O par <see cref="VersaoInvocadaId"/>/<see cref="VersaoInvocadaHash"/> é
/// recebido por valor, completo ou ausente (AC7): Publicações não resolve a
/// versão de configuração — ela vive noutro módulo e chega já resolvida.</para>
/// <para>Retificação e vínculo genérico ato↔entidade não entram aqui — são #800 e #801.</para>
/// </remarks>
public sealed record RegistrarAtoNormativoCommand(
    string Orgao,
    string Serie,
    int Ano,
    string? Numero,
    string TipoCodigo,
    DateOnly DataPublicacao,
    string DocumentoHash,
    string Assinante,
    Guid? VersaoInvocadaId = null,
    string? VersaoInvocadaHash = null) : ICommand<Result<RegistrarAtoNormativoResult>>;

namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// O recibo do envio ou da correção da inscrição: o que o candidato precisa para abrir o comprovante, sem repetir o conteúdo
/// dele. O comprovante se lê em <c>GET /inscricoes/{inscricaoId}/comprovantes/{numeroDoComprovante}</c>.
/// </summary>
/// <param name="InscricaoId">A identidade da inscrição.</param>
/// <param name="NumeroInscricao">O número da inscrição, gerado no primeiro envio.</param>
/// <param name="NumeroDoComprovante">A posição do comprovante emitido na ordem de emissão da inscrição, a partir de 1.</param>
/// <param name="Instante">O instante do envio.</param>
public sealed record ReciboDaInscricaoDto(Guid InscricaoId, string NumeroInscricao, int NumeroDoComprovante, DateTimeOffset Instante);

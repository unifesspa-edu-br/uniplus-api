namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>Dados de inscrição no comprovante.</summary>
public sealed record InscricaoNoComprovanteDto
{
    /// <summary>A inscrição a que o comprovante pertence.</summary>
    public required Guid InscricaoId { get; init; }

    /// <summary>A posição do comprovante na ordem de emissão, a partir de 1: o número com que se lê em <c>GET .../comprovantes/{numero}</c>.</summary>
    public required int Numero { get; init; }

    /// <summary>Quantos comprovantes a inscrição emitiu.</summary>
    public required int Total { get; init; }

    /// <summary>Se é o último emitido, o que vale; os anteriores foram substituídos por correção.</summary>
    public required bool Vale { get; init; }

    /// <summary>O número do comprovante anterior; nulo no primeiro.</summary>
    public int? NumeroAnterior { get; init; }

    /// <summary>O número do comprovante seguinte; nulo no último.</summary>
    public int? NumeroSeguinte { get; init; }

    /// <summary>O envio que o comprovante registra.</summary>
    public required EnvioDoComprovanteDto Envio { get; init; }

    /// <summary>O cancelamento da inscrição; nulo enquanto ela está enviada. Só a emissão pela unidade alcança a inscrição cancelada.</summary>
    public CancelamentoNoComprovanteDto? Cancelamento { get; init; }

    /// <summary>Até quando o candidato pode corrigir a inscrição; nulo quando este não é o comprovante que vale ou o prazo não pôde ser lido.</summary>
    public PrazoDaCorrecaoDto? PrazoDaCorrecao { get; init; }
}

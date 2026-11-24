namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Estado de uma entrada do cache de idempotência. Usado para distinguir
/// reservation (pré-handler) de completed (pós-handler com response cifrada).
/// O código de contrato de cada membro é o valor gravado na coluna <c>status</c>.
/// </summary>
[DescricaoDoContrato("A situação de uma chave de idempotência: reservada enquanto a operação executa, concluída quando a resposta está guardada para repetição.")]
public enum IdempotencyStatus
{
    /// <summary>Reservation criada mas handler ainda em execução.</summary>
    [CodigoDeContrato("EM_PROCESSAMENTO", "Em processamento", Descricao = "A chave foi reservada e a operação ainda está em execução.")]
    Processing = 0,

    /// <summary>Handler completou; response disponível para replay.</summary>
    [CodigoDeContrato("CONCLUIDA", "Concluída", Descricao = "A operação terminou e a resposta está guardada para ser repetida.")]
    Completed = 1,
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que quem atende a providência declara ao revogar a concessão: o motivo da revogação e o e-mail do beneficiário,
/// digitado para confirmar a pessoa. É o mesmo par que a revogação comum pede (UNI-REQ-0376 com UNI-REQ-0186).
/// </summary>
/// <param name="Motivo">O motivo, que vira o motivo da revogação na trilha da concessão.</param>
/// <param name="ConfirmacaoEmail">O e-mail do beneficiário, conferido sem diferenciar maiúsculas nem espaços nas pontas.</param>
public sealed record DeclaracaoDoAtendimento(string? Motivo, string? ConfirmacaoEmail)
{
    /// <summary>O motivo aparado pela regra do motivo da revogação, ou nulo quando inválido.</summary>
    internal string? MotivoLido => Concessao.NormalizarMotivo(Motivo);

    /// <summary>
    /// Acrescenta ao lote a recusa do e-mail que não confirma o beneficiário, com a mesma recusa da revogação comum, e a do
    /// motivo, sem parar na primeira (ADR-0125).
    /// </summary>
    internal void Validar(Concessao concessao, List<FieldError> erros)
    {
        if (concessao.RecusaDaConfirmacao(ConfirmacaoEmail) is { } recusa)
        {
            erros.Add(recusa);
        }

        if (MotivoLido is null)
        {
            erros.Add(Providencia.MotivoInvalido());
        }
    }
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A separação de papéis na decisão: quem é parte do pedido, ou já decidiu como gestor, não decide a etapa seguinte.</summary>
internal readonly record struct SegregacaoDoPedido(
    SolicitanteDoPedido Solicitante, BeneficiarioDoPedido Beneficiario, DecisoesDoPedido Decisoes)
{
    internal void Conferir(Identidade decisor, EtapaDoPedido etapa, List<FieldError> erros)
    {
        if (Solicitante.Eh(decisor) || Beneficiario.Eh(decisor))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.DecisorEParte,
                "Ninguém decide pedido em que é solicitante ou beneficiário.")));
        }

        if (etapa == EtapaDoPedido.ProtecaoDeDados && Decisoes.AprovouComoGestor(decisor))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.MesmoDecisorNasDuasEtapas,
                "Quem decidiu como gestor não decide na etapa de proteção de dados.")));
        }
    }
}

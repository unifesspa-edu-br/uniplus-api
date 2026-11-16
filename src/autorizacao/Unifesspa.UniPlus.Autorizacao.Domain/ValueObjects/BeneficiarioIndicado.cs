namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O beneficiário como a criação o resolveu: o e-mail que o identifica e se é o próprio solicitante.</summary>
internal sealed record BeneficiarioIndicado(string? Email, bool ParaSiMesmo)
{
    /// <summary>O próprio solicitante já nasce identificado; a outra pessoa só se identifica ao aceitar.</summary>
    internal BeneficiarioDoPedido Inicial(SolicitanteDoPedido solicitante) =>
        ParaSiMesmo ? BeneficiarioDoPedido.Conhecido(solicitante.Identidade, solicitante.Nome) : BeneficiarioDoPedido.Desconhecido();
}

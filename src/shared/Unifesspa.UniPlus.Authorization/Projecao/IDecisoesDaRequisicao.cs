namespace Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// As decisões da projeção tomadas na requisição corrente, guardadas onde atravessam o escopo de injeção do Wolverine
/// (o <c>IQueryBus</c> invoca o handler por <c>InvokeAsync</c>, que pode rodar em outro escopo). É por elas que a
/// serialização defensiva decide, sem pedir a decisão de novo (ADR-0153).
/// </summary>
public interface IDecisoesDaRequisicao : IPermissoesDaProjecao
{
    /// <summary>Guarda as decisões permitidas, somadas às que a requisição já tinha.</summary>
    void Guardar(DecisoesDaProjecao decisoes);
}

namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

/// <summary>Textos publicados no contrato para os cinco sinalizadores obrigatórios do tipo de fase.</summary>
internal static class DescricoesDosSinalizadoresDoTipoDeFase
{
    private const string Recusa = " Obrigatório: ausente ou nulo é recusado (422).";

    public const string AgrupaEtapas = "Se o tipo de fase agrupa as etapas pontuadas." + Recusa;

    public const string PermiteComplementacao = "Se o tipo de fase admite complementação documental." + Recusa;

    public const string ColetaInscricao = "Se o tipo de fase coleta inscrições." + Recusa;

    public const string ColetaSolicitacaoIsencao = "Se o tipo de fase coleta a solicitação de isenção da taxa." + Recusa;

    public const string OrganizadaEmChamadas = "Se o tipo de fase é organizada em chamadas." + Recusa;
}

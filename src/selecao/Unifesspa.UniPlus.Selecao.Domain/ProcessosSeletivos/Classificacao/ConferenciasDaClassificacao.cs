namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As conferências dos campos da classificação, que a criação acumula, em vez de parar na primeira
/// (ADR-0125), na ordem em que o operador as corrige. A ausência de se a classificação é baseada em ENEM
/// e da lista de eliminações é recusada primeiro, e as regras que dependem do campo ausente não são conferidas.
/// </summary>
internal static class ConferenciasDaClassificacao
{
    internal static List<FieldError> Conferir(CamposDaClassificacao campos) =>
    [
        .. RecusasDaAusencia(campos),
        .. ConferenciasDoCalculoDaNota.Conferir(campos),
        .. ConferenciasDasEliminacoes.Conferir(campos),
        .. RecusasDoQuadroDePesos(campos),
    ];

    // Quem decide se o quadro é exigido é a fórmula da nota: sem ela confirmada, nada se afirma sobre o quadro.
    private static List<FieldError> RecusasDoQuadroDePesos(CamposDaClassificacao campos) =>
        campos is { RegraCalculo: { } regraCalculo, BaseadoEmEnem: { } baseadoEmEnem }
            ? ConferenciasDoQuadroDePesosPorArea.Conferir(
                ConferenciasDoQuadroDePesosPorArea.Exige(regraCalculo, baseadoEmEnem),
                ConferenciasDoQuadroDePesosPorArea.Aparar(campos.ResolucaoPesoAreaEnem),
                campos.QuadroPesoAreaEnem)
            : [];

    private static List<FieldError> RecusasDaAusencia(CamposDaClassificacao campos)
    {
        List<FieldError> erros = [];
        if (campos.BaseadoEmEnem is null)
        {
            erros.Add(new("baseadoEmEnem", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe se a classificação é baseada em ENEM.")));
        }

        if (campos.Eliminacoes is null)
        {
            erros.Add(new("regrasEliminacao", new DomainError(
                CodigosDeValidacao.Obrigatorio, "Informe as regras de eliminação; envie [] quando não houver nenhuma.")));
        }

        return erros;
    }
}

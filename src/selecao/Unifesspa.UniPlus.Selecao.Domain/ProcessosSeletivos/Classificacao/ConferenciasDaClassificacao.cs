namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As conferências que a criação da classificação acumula, em vez de parar na primeira
/// (ADR-0125), na ordem em que o operador as corrige. A ausência de se a classificação é baseada em ENEM
/// e da lista de eliminações é recusada primeiro, e as regras que dependem do campo ausente não são conferidas.
/// </summary>
internal static class ConferenciasDaClassificacao
{
    internal static List<FieldError> Conferir(DeclaracaoDaClassificacao declaracao) =>
    [
        .. RecusasDaAusencia(declaracao),
        .. ConferenciasDoCalculoDaNota.Conferir(
            declaracao.RegraCalculo,
            declaracao.RegraArredondamento,
            declaracao.CasasArredondamento,
            declaracao.RegrasEliminacao?.Count ?? 0,
            declaracao.EscalaNotaFinal),
        .. RecusasDasEliminacoes(declaracao),
        .. RecusasDoQuadroDePesos(declaracao),
    ];

    private static List<FieldError> RecusasDasEliminacoes(DeclaracaoDaClassificacao declaracao) =>
        declaracao.RegrasEliminacao is { } regras
            ? ConferenciasDasEliminacoes.Conferir(regras, declaracao.RegraCalculo, declaracao.BaseadoEmEnem, declaracao.QuadroPesoAreaEnem)
            : [];

    private static List<FieldError> RecusasDoQuadroDePesos(DeclaracaoDaClassificacao declaracao) =>
        declaracao.BaseadoEmEnem is { } baseadoEmEnem
            ? ConferenciasDoQuadroDePesosPorArea.Conferir(
                ConferenciasDoQuadroDePesosPorArea.Exige(declaracao.RegraCalculo, baseadoEmEnem),
                declaracao.ResolucaoAparada,
                declaracao.QuadroPesoAreaEnem)
            : [];

    private static List<FieldError> RecusasDaAusencia(DeclaracaoDaClassificacao declaracao)
    {
        List<FieldError> erros = [];
        if (declaracao.BaseadoEmEnem is null)
        {
            erros.Add(new("baseadoEmEnem", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe se a classificação é baseada em ENEM.")));
        }

        if (declaracao.RegrasEliminacao is null)
        {
            erros.Add(new("regrasEliminacao", new DomainError(
                CodigosDeValidacao.Obrigatorio, "Informe as regras de eliminação; envie [] quando não houver nenhuma.")));
        }

        return erros;
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As conferências que a criação da classificação acumula, em vez de parar na primeira
/// (ADR-0125), na ordem em que o operador as corrige.
/// </summary>
internal static class ConferenciasDaClassificacao
{
    internal static List<FieldError> Conferir(DeclaracaoDaClassificacao declaracao)
    {
        List<FieldError> erros = ConferenciasDoCalculoDaNota.Conferir(
            declaracao.RegraCalculo,
            declaracao.RegraArredondamento,
            declaracao.CasasArredondamento,
            declaracao.RegrasEliminacao.Count,
            declaracao.EscalaNotaFinal);
        erros.AddRange(ConferenciasDasEliminacoes.Conferir(
            declaracao.RegrasEliminacao, declaracao.RegraCalculo, declaracao.BaseadoEmEnem, declaracao.QuadroPesoAreaEnem));
        erros.AddRange(ConferenciasDoQuadroDePesosPorArea.Conferir(
            ConferenciasDoQuadroDePesosPorArea.Exige(declaracao.RegraCalculo, declaracao.BaseadoEmEnem),
            declaracao.ResolucaoAparada,
            declaracao.QuadroPesoAreaEnem));
        return erros;
    }
}

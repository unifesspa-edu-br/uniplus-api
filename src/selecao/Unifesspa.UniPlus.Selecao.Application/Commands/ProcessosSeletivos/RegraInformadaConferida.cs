namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Um item enviado, sua declaração e recusas, com seus descendentes e posição de origem.</summary>
internal sealed record RegraInformadaConferida(
    RegraDeComprovacaoInformada Entrada,
    DeclaracaoDaRegraDeComprovacao Declaracao,
    string Caminho,
    int Indice,
    IReadOnlyList<FieldError> Recusas,
    IReadOnlyList<RegraInformadaConferida> Itens)
{
    internal static bool RepeticaoRecusada(RegraInformadaConferida regra) =>
        regra.Recusas.Any(static recusa => recusa.Error.Code == "RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
}

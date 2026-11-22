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
    RepeticaoDaRegra Repeticao,
    IReadOnlyList<RegraInformadaConferida> Itens);

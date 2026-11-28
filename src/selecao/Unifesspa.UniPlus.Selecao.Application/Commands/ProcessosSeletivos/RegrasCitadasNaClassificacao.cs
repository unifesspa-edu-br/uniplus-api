namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que o pedido citou do catálogo de regras: a fórmula da nota, a precisão e a ordem de alocação, resolvidas ou
/// ausentes, as eliminações e as recusas do catálogo, cada uma no campo que cita a regra. Só quando tudo se resolveu
/// existe declaração para criar a classificação; com alguma recusa, os campos são conferidos sem ela.
/// </summary>
internal sealed record RegrasCitadasNaClassificacao(
    ReferenciaRegra? Calculo,
    ReferenciaRegra? Arredondamento,
    ReferenciaRegra? OrdemDeAlocacao,
    IReadOnlyList<FieldError> RecusasDasRegrasDaNota,
    EliminacoesResolvidas Eliminacoes)
{
    [MemberNotNullWhen(true, nameof(Calculo), nameof(OrdemDeAlocacao))]
    public bool TodasResolvidas =>
        Calculo is not null && OrdemDeAlocacao is not null && RecusasDasRegrasDaNota.Count == 0 && Eliminacoes.SemRecusa;

    /// <summary>Na ordem do pedido: parâmetros das eliminações, depois o catálogo (fórmula, precisão, ordem, eliminações).</summary>
    public IReadOnlyList<FieldError> Recusas =>
    [
        .. Eliminacoes.RecusasDosParametros,
        .. RecusasDasRegrasDaNota,
        .. Eliminacoes.RecusasDoCatalogo,
    ];

    /// <summary>O caminho em que tudo se resolveu: a declaração que <see cref="ConfiguracaoClassificacao.Criar"/> confere.</summary>
    public DeclaracaoDaClassificacao Declaracao(DefinirClassificacaoCommand command, QuadroDePesosResolvido quadro)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(quadro);
        if (!TodasResolvidas)
        {
            throw new InvalidOperationException("A declaração só existe quando todas as regras citadas foram resolvidas.");
        }

        return quadro.DeclaracaoCompleta(DeclaracaoDasRegras(command, Calculo, OrdemDeAlocacao));
    }

    private DeclaracaoDaClassificacao DeclaracaoDasRegras(DefinirClassificacaoCommand command, ReferenciaRegra calculo, ReferenciaRegra ordemDeAlocacao) =>
        new()
        {
            RegraCalculo = calculo,
            RegraArredondamento = Arredondamento,
            CasasArredondamento = command.CasasArredondamento,
            RegraOrdemAlocacao = ordemDeAlocacao,
            RegrasEliminacao = Eliminacoes.Criadas,
            BaseadoEmEnem = command.BaseadoEmEnem,
            EscalaNotaFinal = command.EscalaNotaFinal,
        };

    /// <summary>O pedido com recusa no catálogo: o que foi declarado, para conferir o que não depende da parte recusada.</summary>
    public CamposDaClassificacao Campos(DefinirClassificacaoCommand command, QuadroDePesosResolvido quadro)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(quadro);

        return new()
        {
            RegraCalculo = Calculo,
            ArredondamentoDeclarado = command.RegraArredondamentoCodigo is not null,
            CasasArredondamento = command.CasasArredondamento,
            Eliminacoes = Eliminacoes.Declaradas,
            BaseadoEmEnem = command.BaseadoEmEnem,
            ResolucaoPesoAreaEnem = quadro.Resolucao,
            QuadroPesoAreaEnem = quadro.Quadro,
            EscalaNotaFinal = command.EscalaNotaFinal,
        };
    }
}

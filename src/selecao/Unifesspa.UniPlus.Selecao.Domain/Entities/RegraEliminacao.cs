namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma regra de eliminação por cálculo da <see cref="ConfiguracaoClassificacao"/>:
/// referencia uma regra tipada do
/// <c>regras_do_catalogo</c> (<c>tipo=regra_eliminacao</c>) e seus parâmetros aplicados.
/// Cardinalidade múltipla — ex.: o PS Convênios exige duas
/// <c>ELIMINACAO_NOTA_MINIMA_ETAPA</c> independentes (Objetiva e Redação).
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>/<see cref="CriterioDesempate"/>: a configuração
/// em rascunho é substituível por inteiro
/// (<see cref="ConfiguracaoClassificacao.Criar"/>).
/// </remarks>
public sealed class RegraEliminacao : EntityBase
{
    public Guid ConfiguracaoClassificacaoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public ParametrosRegraEliminacao Parametros { get; private set; } = null!;

    private RegraEliminacao() { }

    /// <summary>
    /// Cria a regra de eliminação validando que <paramref name="parametros"/> é a
    /// variante correta para o <see cref="ReferenciaRegra.Codigo"/>
    /// referenciado — a existência do <c>etapa_ref</c> no processo é
    /// validada pela raiz, que tem acesso às etapas.
    /// </summary>
    public static Result<RegraEliminacao> Criar(ReferenciaRegra regra, ParametrosRegraEliminacao parametros)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(parametros);

        DomainError? recusa = regra.Codigo == parametros.RegraCorrespondente().Codigo()
            ? parametros.RecusaDosValores()
            : new DomainError("RegraEliminacao.ParametrosIncompativeisComRegra",
                "Os parâmetros informados não são os da regra de eliminação escolhida. Use só os parâmetros que ela pede.");
        return recusa is null
            ? Result<RegraEliminacao>.Success(new RegraEliminacao { Regra = regra, Parametros = parametros })
            : Result<RegraEliminacao>.Failure(recusa);
    }

    internal void VincularConfiguracao(Guid configuracaoClassificacaoId) =>
        ConfiguracaoClassificacaoId = configuracaoClassificacaoId;
}

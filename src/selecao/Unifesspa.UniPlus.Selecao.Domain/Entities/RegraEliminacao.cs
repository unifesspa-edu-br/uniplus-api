namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma regra de eliminação por cálculo da <see cref="ConfiguracaoClassificacao"/>
/// (Story #775, modelagem P-B §2.5): referencia uma regra tipada do
/// <c>rol_de_regras</c> (<c>tipo=regra_eliminacao</c>) e seus args aplicados.
/// Cardinalidade múltipla — ex.: o PS Convênios exige duas
/// <c>ELIM-NOTA-MINIMA-ETAPA</c> independentes (Objetiva e Redação).
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
    public ArgsRegraEliminacao Args { get; private set; } = null!;

    private RegraEliminacao() { }

    /// <summary>
    /// Cria a regra de eliminação validando que <paramref name="args"/> é a
    /// variante correta para o <see cref="ReferenciaRegra.Codigo"/>
    /// referenciado — a existência do <c>etapa_ref</c> no processo (INV-B4) é
    /// validada pela raiz, que tem acesso às etapas.
    /// </summary>
    public static Result<RegraEliminacao> Criar(ReferenciaRegra regra, ArgsRegraEliminacao args)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);

        bool argsCompativeis = regra.Codigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => args is ArgsElimNotaMinimaEtapa,
            RegraEliminacaoCodigo.ElimCorteRedacao => args is ArgsElimCorteRedacao,
            RegraEliminacaoCodigo.ElimZeroEmArea => args is ArgsElimZeroEmArea,
            _ => false,
        };

        if (!argsCompativeis)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.ArgsIncompativeisComRegra",
                $"Os args informados não correspondem à regra {regra.Codigo}."));
        }

        if (args is ArgsElimNotaMinimaEtapa { NotaMinima: < 0 })
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.NotaMinimaInvalida", "A nota mínima da eliminação deve ser não negativa."));
        }

        if (args is ArgsElimCorteRedacao { Minimo: < 0 })
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.MinimoInvalido", "O mínimo do corte de redação deve ser não negativo."));
        }

        return Result<RegraEliminacao>.Success(new RegraEliminacao { Regra = regra, Args = args });
    }

    internal void VincularConfiguracao(Guid configuracaoClassificacaoId) =>
        ConfiguracaoClassificacaoId = configuracaoClassificacaoId;
}

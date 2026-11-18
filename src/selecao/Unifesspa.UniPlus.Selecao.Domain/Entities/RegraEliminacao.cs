namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma regra de eliminação por cálculo da <see cref="ConfiguracaoClassificacao"/>
/// (Story #775): referencia uma regra tipada do
/// <c>regras_do_catalogo</c> (<c>tipo=regra_eliminacao</c>) e seus parâmetros aplicados.
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

        bool parametrosCompativeis = regra.Codigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => parametros is ParametrosElimNotaMinimaEtapa,
            RegraEliminacaoCodigo.ElimCorteEmArea => parametros is ParametrosElimCorteEmArea,
            RegraEliminacaoCodigo.ElimZeroEmArea => parametros is ParametrosElimZeroEmArea,
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => parametros is ParametrosElimFaltaEmDiaDeProvaEnem,
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa => parametros is ParametrosElimAusenciaNaEtapa,
            _ => false,
        };

        if (!parametrosCompativeis)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.ParametrosIncompativeisComRegra",
                $"Os parâmetros informados não correspondem à regra {regra.Codigo}."));
        }

        if (parametros is ParametrosElimNotaMinimaEtapa { NotaMinima: < 0 })
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.NotaMinimaInvalida", "A nota mínima da eliminação deve ser não negativa."));
        }

        if (parametros is ParametrosElimCorteEmArea corte)
        {
            if (!CriterioDesempate.CodigoDeAreaValido(corte.AreaCodigo))
            {
                return Result<RegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.AreaInvalida",
                    $"A área do corte é um código de até {GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength} caracteres, com letras maiúsculas sem acento, algarismos e sublinhado."));
            }

            if (corte.Minimo < 0)
            {
                return Result<RegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.MinimoInvalido", "O mínimo do corte em área deve ser não negativo."));
            }
        }

        return Result<RegraEliminacao>.Success(new RegraEliminacao { Regra = regra, Parametros = parametros });
    }

    internal void VincularConfiguracao(Guid configuracaoClassificacaoId) =>
        ConfiguracaoClassificacaoId = configuracaoClassificacaoId;
}

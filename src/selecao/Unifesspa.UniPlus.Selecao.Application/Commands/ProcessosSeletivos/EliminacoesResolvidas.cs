namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ProcessosSeletivos.Classificacao;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// As eliminações que o pedido declarou, resolvidas uma a uma na posição em que vieram: a regra no catálogo, depois os
/// parâmetros e a criação. Guarda as eliminações criadas, os parâmetros montados em cada posição (vazia onde o item foi
/// recusado) e as recusas, separadas por origem porque a conferência as ordena em momentos diferentes.
/// </summary>
internal sealed class EliminacoesResolvidas
{
    private readonly ParametrosRegraEliminacao?[]? _parametrosPorPosicao;
    private readonly List<RegraEliminacao> _criadas = [];
    private readonly List<FieldError> _recusasDosParametros = [];
    private readonly List<FieldError> _recusasDoCatalogo = [];

    private EliminacoesResolvidas(int? quantidade) =>
        _parametrosPorPosicao = quantidade is { } total ? new ParametrosRegraEliminacao?[total] : null;

    public IReadOnlyList<RegraEliminacao>? Criadas => _parametrosPorPosicao is null ? null : _criadas;

    public EliminacoesDeclaradas? Declaradas => _parametrosPorPosicao is null ? null : EliminacoesDeclaradas.NasPosicoes(_parametrosPorPosicao);

    public IReadOnlyList<FieldError> RecusasDosParametros => _recusasDosParametros;

    public IReadOnlyList<FieldError> RecusasDoCatalogo => _recusasDoCatalogo;

    public bool SemRecusa => _recusasDosParametros.Count == 0 && _recusasDoCatalogo.Count == 0;

    /// <summary>A lista ausente segue nula para a criação da classificação, que a recusa junto das demais recusas.</summary>
    public static EliminacoesResolvidas NaoInformadas() => new(null);

    public static EliminacoesResolvidas ParaQuantidade(int quantidade) => new(quantidade);

    public void Acrescentar(int posicao, RegraCatalogo? regra, RegraEliminacaoInput input)
    {
        if (RecusaDaRegra(regra) is { } recusaDoCatalogo)
        {
            _recusasDoCatalogo.Add(new(null, recusaDoCatalogo));
        }
        else
        {
            AcrescentarComParametros(posicao, regra!, input);
        }
    }

    private void AcrescentarComParametros(int posicao, RegraCatalogo regra, RegraEliminacaoInput input)
    {
        Result<RegraEliminacao> criada = Criar(regra, input);
        if (criada.IsFailure)
        {
            _recusasDosParametros.Add(new(null, criada.Error!));
        }
        else
        {
            Adotar(posicao, criada.Value!);
        }
    }

    private void Adotar(int posicao, RegraEliminacao criada)
    {
        _parametrosPorPosicao![posicao] = criada.Parametros;
        _criadas.Add(criada);
    }

    private static Result<RegraEliminacao> Criar(RegraCatalogo regra, RegraEliminacaoInput input)
    {
        Result<ParametrosRegraEliminacao> parametros = ParametrosDaEliminacaoDeclarados.Montar(input);
        return parametros.IsFailure
            ? Result<RegraEliminacao>.Failure(parametros.Error!)
            : RegraEliminacao.Criar(regra.Referencia(), parametros.Value!);
    }

    private static DomainError? RecusaDaRegra(RegraCatalogo? regra) => regra switch
    {
        null => new DomainError("RegraEliminacao.RegraNaoEncontrada",
            "Regra de eliminação não encontrada no catálogo de regras."),
        { Tipo: not TipoRegra.RegraEliminacao } => new DomainError("RegraEliminacao.RegraTipoInvalido",
            "A regra informada não é uma regra de eliminação."),
        _ => null,
    };
}

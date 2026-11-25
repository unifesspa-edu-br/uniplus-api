namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Uma regra do pedido, conferida com as recusas acumuladas (ADR-0125). A forma da regra (ordem e código
/// contribuído) e a forma das condições não dependem uma da outra e saem juntas. A semântica do predicado, contra o
/// vocabulário e a oferta do processo, depende do predicado bem formado e só é conferida quando as
/// condições vieram sem recusa. Os campos vêm relativos à regra: <c>ordem</c>, <c>contribui</c> e
/// <c>quando[cláusula][condição].campo</c>.
/// </summary>
internal sealed class RegraDeDerivacaoInformada
{
    private readonly RegraDerivacaoInput _entrada;
    private readonly List<(int Clausula, CondicaoDnf Condicao)> _linhas = [];
    private readonly PredicadoDnf? _predicado;

    private RegraDeDerivacaoInformada(RegraDerivacaoInput entrada)
    {
        _entrada = entrada;
        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = EntradaDeRegras.Condicoes(entrada.Quando);
        List<FieldError> recusas = [.. RegraDerivacaoConfigurada.ValidarFormaBasica(entrada.Ordem, entrada.Contribui)];
        recusas.AddRange(linhas.Errors.Select(NoPredicado));
        if (linhas.IsSuccess)
        {
            _linhas = linhas.Value!;
            Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(_linhas);
            _predicado = _linhas.Count > 0 && predicado.IsSuccess ? predicado.Value : null;
            recusas.AddRange(predicado.Errors.Select(static e => new FieldError("quando", e.Error)));
        }

        RecusasDeForma = recusas;
    }

    /// <summary>As recusas de forma da regra e das condições, na ordem em que o pedido as traz.</summary>
    public IReadOnlyList<FieldError> RecusasDeForma { get; }

    public static RegraDeDerivacaoInformada De(RegraDerivacaoInput entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        return new RegraDeDerivacaoInformada(entrada);
    }

    /// <summary>
    /// A semântica do predicado: fato citado no vocabulário e disponível na configuração final, operador e valor
    /// do domínio do fato. Vazia quando as condições vieram mal formadas, e na regra incondicional, que não tem condição.
    /// </summary>
    public IReadOnlyList<FieldError> RecusasDeSemantica(
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        _predicado is null
            ? []
            : [.. PredicadoDnfValidador.Validar(_predicado, vocabulario, universo, dominiosDinamicos).Errors.Select(NoPredicado)];

    /// <summary>A regra como se grava. Só vale para a regra sem recusa de forma.</summary>
    public Result<RegraDerivacaoConfigurada> Montar()
    {
        List<CondicaoRegraDerivacao> condicoes = [];
        foreach ((int clausula, CondicaoDnf condicao) in _linhas)
        {
            Result<CondicaoRegraDerivacao> criada = CondicaoRegraDerivacao.Criar(clausula, condicao.Fato, condicao.Operador, condicao.Valor);
            if (criada.IsFailure)
            {
                return Result<RegraDerivacaoConfigurada>.Failure(criada.Error!);
            }

            condicoes.Add(criada.Value!);
        }

        return RegraDerivacaoConfigurada.Criar(_entrada.Ordem, _entrada.Contribui, condicoes);
    }

    private static FieldError NoPredicado(FieldError recusa) => recusa.NoCaminhoDoItem("quando");
}

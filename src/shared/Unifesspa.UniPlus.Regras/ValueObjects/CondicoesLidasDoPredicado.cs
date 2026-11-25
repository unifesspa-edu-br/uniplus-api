namespace Unifesspa.UniPlus.Regras.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

internal sealed class CondicoesLidasDoPredicado
{
    private readonly List<(int Clausula, CondicaoDnf Condicao)> _condicoes = [];
    private readonly List<FieldError> _recusas = [];

    internal void RecusarClausula(int clausula, DomainError recusa) =>
        _recusas.Add(new FieldError($"[{clausula}]", recusa));

    internal void AcrescentarCondicao(int clausula, int posicao, Result<CondicaoDnf> lida)
    {
        _recusas.AddRange(lida.Errors.Select(erro => erro.NoCaminhoDe($"[{clausula}][{posicao}]")));
        if (lida.IsSuccess)
        {
            _condicoes.Add((clausula, lida.Value!));
        }
    }

    internal Result<List<(int Clausula, CondicaoDnf Condicao)>> Condicoes() =>
        _recusas.Count > 0
            ? Result<List<(int Clausula, CondicaoDnf Condicao)>>.ValidationFailure(_recusas)
            : Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(_condicoes);

    internal Result<PredicadoDnf> Predicado() => _recusas.Count > 0
        ? Result<PredicadoDnf>.ValidationFailure(_recusas)
        : PredicadoDnf.CriarDeCondicoesAgrupadas(_condicoes);
}

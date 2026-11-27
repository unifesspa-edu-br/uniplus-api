namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A conversão de uma derivação do formulário portável. Todas as regras são lidas, e o código do fato é conferido
/// mesmo quando uma regra é recusada; a derivação só é criada sem nenhuma recusa, porque a criação depende das regras
/// e do código do fato lidos.
/// </summary>
internal static class ConversaoDaDerivacaoPortavel
{
    internal static Result<DerivacaoDoFato> Derivacao(DerivacaoPortavel derivacao)
    {
        RecusasDoElemento recusas = new();
        List<RegraDerivacao> regras = recusas.Lista(derivacao.Regras, "regras", regra => Regra(derivacao.Booleano, regra!));
        recusas.Recusar(ConversaoDoPortavel.Estrutura(DerivacaoDoFato.Violacao(derivacao.FatoCodigo)), "fatoCodigo");

        return recusas.ConferidoPor(() => DerivacaoDasRegras(derivacao, regras));
    }

    private static Result<RegraDerivacao> Regra(bool booleano, RegraDerivacaoPortavel regra)
    {
        if (regra.Quando is null)
        {
            return Result<RegraDerivacao>.ValidationFailure([new FieldError("quando", new DomainError(
                FormularioPortavelErrorCodes.DerivacaoInvalida,
                "Uma regra da derivação não diz quando ativa; a regra sempre ativa tem a lista vazia."))]);
        }

        RecusasDoElemento recusas = new();
        PredicadoDnf? quando = recusas.Parte(RegrasDoPortavel.Predicado(regra.Quando), "quando");

        return recusas.ConferidoPor(() => ComContribuicao(booleano, quando!, regra.Contribui));
    }

    private static Result<RegraDerivacao> ComContribuicao(bool booleano, PredicadoDnf quando, string? contribui)
    {
        RecusasDoElemento recusas = new();
        RegraDerivacao? regra = recusas.Parte(
            booleano ? Result<RegraDerivacao>.Success(RegraDerivacao.CriarBooleana(quando)) : RegraDerivacao.Criar(quando, contribui ?? string.Empty),
            "contribui");

        return recusas.Montado(() => regra!);
    }

    /// <summary>
    /// As dependências são as que as regras citam, e o domínio do categórico é o que elas contribuem: o
    /// avaliador não usa mais que isso do derivado.
    /// </summary>
    private static Result<DerivacaoDoFato> DerivacaoDasRegras(DerivacaoPortavel derivacao, List<RegraDerivacao> regras)
    {
        string[] dependencias = [.. regras.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];
        Result<DerivacaoDoFato> lida = derivacao.Booleano
            ? DerivacaoDoFato.CriarBooleana(derivacao.FatoCodigo, regras, dependencias)
            : DerivacaoDoFato.Criar(
                derivacao.FatoCodigo, regras, dependencias, [.. regras.Select(static r => r.Contribui!).Distinct(StringComparer.Ordinal)]);
        return lida.IsSuccess
            ? lida
            : Result<DerivacaoDoFato>.Failure(new DomainError(FormularioPortavelErrorCodes.DerivacaoInvalida, lida.Error!.Message));
    }
}

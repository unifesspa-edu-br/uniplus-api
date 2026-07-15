namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Domain service estático que confere um <see cref="PredicadoDnf"/> contra o
/// vocabulário fechado de fatos do candidato (ADR-0111, Story #847): aplica a
/// matriz de compatibilidade operador × domínio e, quando o domínio é
/// categórico estático, o pertencimento do valor ao domínio declarado. Puro —
/// não faz I/O; <paramref name="vocabularioFechado"/> é fornecido já
/// resolvido por quem chama (Application), a partir do leitor cross-módulo do
/// catálogo de fatos (#846).
/// </summary>
public static class PredicadoDnfValidador
{
    /// <summary>
    /// Percorre toda condição de toda cláusula do predicado; a primeira
    /// falha interrompe a validação (early-return, mesmo estilo de
    /// <see cref="ObrigatoriedadeLegalPayloadNormalizer"/>).
    /// </summary>
    /// <param name="predicado">O predicado a validar.</param>
    /// <param name="vocabularioFechado">O vocabulário fechado de fatos, por código.</param>
    /// <param name="fatosColetadosPeloProcesso">
    /// Quando informado, toda condição cujo <c>Fato</c> não pertença a este
    /// conjunto reprova com um erro distinto do de fato desconhecido no
    /// vocabulário. Quando omitido (<see langword="null"/>), esta checagem
    /// adicional não se aplica.
    /// </param>
    public static Result Validar(
        PredicadoDnf predicado,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabularioFechado,
        IReadOnlySet<string>? fatosColetadosPeloProcesso = null)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        ArgumentNullException.ThrowIfNull(vocabularioFechado);

        foreach (ClausulaDnf clausula in predicado.Clausulas)
        {
            foreach (CondicaoDnf condicao in clausula.Condicoes)
            {
                Result condicaoResult = ValidarCondicao(condicao, vocabularioFechado, fatosColetadosPeloProcesso);
                if (condicaoResult.IsFailure)
                {
                    return condicaoResult;
                }
            }
        }

        return Result.Success();
    }

    private static Result ValidarCondicao(
        CondicaoDnf condicao,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabularioFechado,
        IReadOnlySet<string>? fatosColetadosPeloProcesso)
    {
        if (!vocabularioFechado.TryGetValue(condicao.Fato, out DescritorFatoCandidato? descritor))
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.FatoDesconhecido",
                $"O fato '{condicao.Fato}' não pertence ao vocabulário fechado."));
        }

        if (fatosColetadosPeloProcesso is not null && !fatosColetadosPeloProcesso.Contains(condicao.Fato))
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.FatoNaoColetadoPeloProcesso",
                $"O fato '{condicao.Fato}' não é coletado por este processo."));
        }

        Result operadorResult = ValidarOperador(condicao, descritor);
        return operadorResult.IsFailure ? operadorResult : ValidarValor(condicao, descritor);
    }

    private static Result ValidarOperador(CondicaoDnf condicao, DescritorFatoCandidato descritor)
    {
        bool operadorCompativel = descritor.TipoDominio switch
        {
            TipoDominioFato.Booleano => condicao.Operador == Operador.Igual,
            TipoDominioFato.Numerico => condicao.Operador is Operador.Igual or Operador.MaiorIgual or Operador.MenorIgual,
            TipoDominioFato.CategoricoEstatico => condicao.Operador is Operador.Igual or Operador.Em,
            _ => false,
        };

        return operadorCompativel
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.OperadorIncompativelComDominio",
                $"O operador {condicao.Operador} não é compatível com o domínio {descritor.TipoDominio} do fato '{condicao.Fato}'."));
    }

    private static Result ValidarValor(CondicaoDnf condicao, DescritorFatoCandidato descritor) => descritor.TipoDominio switch
    {
        TipoDominioFato.Booleano => ValidarValorBooleano(condicao),
        TipoDominioFato.Numerico => ValidarValorNumerico(condicao),
        TipoDominioFato.CategoricoEstatico => ValidarValorCategorico(condicao, descritor),
        _ => Result.Failure(new DomainError("PredicadoDnf.ValorIncompativelComTipo", "Domínio do fato desconhecido.")),
    };

    private static Result ValidarValorBooleano(CondicaoDnf condicao) =>
        condicao.Valor.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.ValorIncompativelComTipo",
                $"O valor da condição sobre '{condicao.Fato}' deve ser um booleano JSON."));

    private static Result ValidarValorNumerico(CondicaoDnf condicao)
    {
        if (condicao.Valor.ValueKind != JsonValueKind.Number)
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.ValorIncompativelComTipo",
                $"O valor da condição sobre '{condicao.Fato}' deve ser um número JSON."));
        }

        return condicao.Valor.TryGetInt64(out _)
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.ValorIncompativelComTipo",
                $"O valor da condição sobre '{condicao.Fato}' deve ser um número inteiro (decimal é rejeitado)."));
    }

    private static Result ValidarValorCategorico(CondicaoDnf condicao, DescritorFatoCandidato descritor)
    {
        IReadOnlyList<string> dominio = descritor.ValoresDominio!;

        if (condicao.Operador == Operador.Em)
        {
            return ValidarValorCategoricoEm(condicao, dominio);
        }

        if (condicao.Valor.ValueKind != JsonValueKind.String)
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.ValorIncompativelComTipo",
                $"O valor da condição sobre '{condicao.Fato}' deve ser uma string JSON."));
        }

        return dominio.Contains(condicao.Valor.GetString(), StringComparer.Ordinal)
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.ValorForaDoDominio",
                $"O valor da condição sobre '{condicao.Fato}' não pertence ao domínio declarado."));
    }

    private static Result ValidarValorCategoricoEm(CondicaoDnf condicao, IReadOnlyList<string> dominio)
    {
        foreach (JsonElement item in condicao.Valor.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return Result.Failure(new DomainError(
                    "PredicadoDnf.ValorIncompativelComTipo",
                    $"Os valores da condição EM sobre '{condicao.Fato}' devem ser strings JSON."));
            }
        }

        string? foraDoDominio = condicao.Valor.EnumerateArray()
            .Select(static item => item.GetString())
            .FirstOrDefault(valor => !dominio.Contains(valor, StringComparer.Ordinal));

        return foraDoDominio is null
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.ValorForaDoDominio",
                $"O valor '{foraDoDominio}' da condição EM sobre '{condicao.Fato}' não pertence ao domínio declarado."));
    }
}

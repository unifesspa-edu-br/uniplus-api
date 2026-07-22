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
    /// <param name="dominiosDinamicos">
    /// Domínio dinâmico por fato (Story #554, PR #896) — obrigatório quando o predicado
    /// cita um fato <see cref="TipoDominioFato.CategoricoDinamico"/> (ex.: <c>MODALIDADE</c>,
    /// <c>CONDICAO_ATENDIMENTO</c>). O chamador (Application) o deriva da oferta do próprio
    /// processo — este validador não sabe de onde vem, só que precisa ser fornecido.
    /// </param>
    public static Result Validar(
        PredicadoDnf predicado,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabularioFechado,
        IReadOnlySet<string>? fatosColetadosPeloProcesso = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos = null)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        ArgumentNullException.ThrowIfNull(vocabularioFechado);

        foreach (ClausulaDnf clausula in predicado.Clausulas)
        {
            foreach (CondicaoDnf condicao in clausula.Condicoes)
            {
                Result condicaoResult = ValidarCondicao(condicao, vocabularioFechado, fatosColetadosPeloProcesso, dominiosDinamicos);
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
        IReadOnlySet<string>? fatosColetadosPeloProcesso,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos)
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
        return operadorResult.IsFailure ? operadorResult : ValidarValor(condicao, descritor, dominiosDinamicos);
    }

    private static Result ValidarOperador(CondicaoDnf condicao, DescritorFatoCandidato descritor)
    {
        // Story #916: DIFERENTE acompanha IGUAL em todo domínio onde IGUAL já vale — inclusive
        // booleano/numérico (a letra da spec: "DIFERENTE só onde IGUAL vale", sem exceção de
        // domínio). NAO_EM acompanha EM, que só vale nos dois ramos categóricos — booleano e
        // numérico nunca tiveram EM.
        bool operadorCompativel = descritor.TipoDominio switch
        {
            TipoDominioFato.Booleano => condicao.Operador is Operador.Igual or Operador.Diferente,
            TipoDominioFato.Numerico => condicao.Operador is Operador.Igual or Operador.MaiorIgual or Operador.MenorIgual or Operador.Diferente,
            TipoDominioFato.CategoricoEstatico => condicao.Operador is Operador.Igual or Operador.Em or Operador.Diferente or Operador.NaoEm,
            TipoDominioFato.CategoricoDinamico => condicao.Operador is Operador.Igual or Operador.Em or Operador.Diferente or Operador.NaoEm,
            _ => false,
        };

        return operadorCompativel
            ? Result.Success()
            : Result.Failure(new DomainError(
                "PredicadoDnf.OperadorIncompativelComDominio",
                $"O operador {condicao.Operador} não é compatível com o domínio {descritor.TipoDominio} do fato '{condicao.Fato}'."));
    }

    private static Result ValidarValor(
        CondicaoDnf condicao,
        DescritorFatoCandidato descritor,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos) => descritor.TipoDominio switch
        {
            TipoDominioFato.Booleano => ValidarValorBooleano(condicao),
            TipoDominioFato.Numerico => ValidarValorNumerico(condicao),
            TipoDominioFato.CategoricoEstatico => ValidarValorCategorico(condicao, descritor.ValoresDominio!),
            TipoDominioFato.CategoricoDinamico => ValidarValorCategoricoDinamico(condicao, dominiosDinamicos),
            _ => Result.Failure(new DomainError("PredicadoDnf.ValorIncompativelComTipo", "Domínio do fato desconhecido.")),
        };

    /// <summary>
    /// CA-03 (integridade referencial, Story #554): o domínio válido de um fato
    /// categórico dinâmico vem do que o PRÓPRIO PROCESSO oferece (modalidades
    /// selecionadas, condições de atendimento ofertadas) — nunca de um catálogo global.
    /// Ausência do fato em <paramref name="dominiosDinamicos"/> é erro do CHAMADOR (nunca
    /// confia silenciosamente), distinto de "valor fora do domínio".
    /// </summary>
    private static Result ValidarValorCategoricoDinamico(
        CondicaoDnf condicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos)
    {
        if (dominiosDinamicos is null || !dominiosDinamicos.TryGetValue(condicao.Fato, out IReadOnlySet<string>? dominio))
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.DominioDinamicoNaoFornecido",
                $"O domínio dinâmico do fato '{condicao.Fato}' não foi fornecido — o chamador precisa derivá-lo da oferta do processo."));
        }

        return ValidarValorCategorico(condicao, [.. dominio]);
    }

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

    private static Result ValidarValorCategorico(CondicaoDnf condicao, IReadOnlyList<string> dominio)
    {
        // NAO_EM (Story #916) segue a mesma forma de lista de EM — negação, não muda a
        // validação de valor × domínio.
        if (condicao.Operador is Operador.Em or Operador.NaoEm)
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
        bool possuiItemNaoString = condicao.Valor.EnumerateArray()
            .Any(static item => item.ValueKind != JsonValueKind.String);

        if (possuiItemNaoString)
        {
            return Result.Failure(new DomainError(
                "PredicadoDnf.ValorIncompativelComTipo",
                $"Os valores da condição EM sobre '{condicao.Fato}' devem ser strings JSON."));
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

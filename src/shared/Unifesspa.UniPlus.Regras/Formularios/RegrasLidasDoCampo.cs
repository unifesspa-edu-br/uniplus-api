namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras do campo como a leitura da entrada as devolve, quando alguma parte pode ter sido recusada: a parte nula
/// não foi lida, já tem a recusa dela, e conta como conforme. A restrição nula guarda a posição da recusada, para
/// as demais seguirem no índice do pedido. Cada regra é conferida aqui, uma única vez, tenha o campo sido lido por
/// inteiro ou não.
/// </summary>
/// <param name="Exibicao">Quando o campo aparece; nulo quando aparece sempre ou quando foi recusada.</param>
/// <param name="Obrigatoriedade">Nula quando recusada.</param>
/// <param name="Restricoes">Na ordem do pedido; a recusada é nula.</param>
/// <param name="Impedimento">Nulo quando não há ou quando foi recusado.</param>
public sealed record RegrasLidasDoCampo(
    PredicadoDnf? Exibicao,
    Obrigatoriedade? Obrigatoriedade,
    IReadOnlyList<RestricaoValor?> Restricoes,
    Impedimento? Impedimento)
{
    /// <summary>As regras de um campo lido por inteiro.</summary>
    public static RegrasLidasDoCampo De(RegrasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return new(regras.Exibicao, regras.Obrigatoriedade, [.. regras.Restricoes], regras.Impedimento);
    }

    /// <summary>
    /// Nenhuma regra, além do impedimento, cita o próprio fato, as restrições cabem no tipo do campo e o impedimento é
    /// coerente com ele. A autorreferência é ciclo de comprimento um, recusada aqui sem precisar conhecer os outros
    /// campos. Sem o tipo do campo, não se confere o que depende dele: restrição e impedimento fora do tipo e município
    /// sem UF.
    /// </summary>
    public IEnumerable<FieldError> ConferirNoCampo(string fatoDoCampo, TipoDeCampo? tipo) =>
    [
        .. RecusasDaExibicao(fatoDoCampo),
        .. RecusasDaObrigatoriedade(fatoDoCampo),
        .. RecusasDasRestricoes(fatoDoCampo, tipo),
        .. RecusasDoImpedimento(fatoDoCampo, tipo),
    ];

    private IEnumerable<FieldError> RecusasDaExibicao(string fatoDoCampo)
    {
        if ((Exibicao?.FatosCitados ?? []).Contains(fatoDoCampo, StringComparer.Ordinal))
        {
            yield return new("exibicao", new DomainError(ItemFormularioErrorCodes.RegraAutorreferente, "A exibição cita o próprio fato."));
        }
    }

    private IEnumerable<FieldError> RecusasDaObrigatoriedade(string fatoDoCampo)
    {
        if ((Obrigatoriedade?.FatosCitados ?? []).Contains(fatoDoCampo, StringComparer.Ordinal))
        {
            yield return new("obrigatoriedade.predicado", new DomainError(
                ItemFormularioErrorCodes.RegraAutorreferente, "A obrigatoriedade cita o próprio fato."));
        }
    }

    /// <summary>
    /// As restrições cabem no tipo do campo — faixa só no numérico, tamanho só no de texto, opções
    /// só no de seleção, municípios da UF só no de município, onde são obrigatórios, faixa de data só
    /// no de data —, não se repetem por tipo, não citam o próprio fato, e os limites da faixa cabem
    /// nas casas decimais do edital.
    /// </summary>
    private IEnumerable<FieldError> RecusasDasRestricoes(string fatoDoCampo, TipoDeCampo? tipo) =>
    [
        .. RecusasDoConjuntoDeRestricoes(tipo),
        .. Restricoes.SelectMany((restricao, indice) => RecusasDaRestricao(fatoDoCampo, tipo, restricao, indice)),
    ];

    private IEnumerable<FieldError> RecusasDoConjuntoDeRestricoes(TipoDeCampo? tipo)
    {
        if (RestricoesDeValor.TipoRepetido(Restricoes.OfType<RestricaoValor>()) is { } repetido)
        {
            yield return new("restricoes", repetido);
        }

        if (MunicipioSemUf(tipo))
        {
            yield return new("restricoes", new DomainError(
                ItemFormularioErrorCodes.MunicipioSemUf, "O campo de município cita a UF respondida antes, pelos municípios da UF."));
        }
    }

    private bool MunicipioSemUf(TipoDeCampo? tipo) =>
        tipo == TipoDeCampo.Municipio
        && Restricoes.All(static r => r is not null)
        && !Restricoes.Any(static r => r is MunicipiosDaUf);

    private static IEnumerable<FieldError> RecusasDaRestricao(string fatoDoCampo, TipoDeCampo? tipo, RestricaoValor? restricao, int indice) =>
        restricao is null
            ? []
            :
            [
                .. RecusasDeRestricaoForaDoTipoDoCampo(tipo, restricao, indice),
                .. RecusasDeCasasDecimaisDaFaixa(restricao, indice),
                .. RecusasDeRestricaoQueCitaOProprioFato(fatoDoCampo, restricao, indice),
            ];

    private static IEnumerable<FieldError> RecusasDeRestricaoForaDoTipoDoCampo(TipoDeCampo? tipo, RestricaoValor restricao, int indice)
    {
        if (tipo is { } campo && !FormaDoItem.RestricaoCabeNoCampo(restricao.Tipo, campo))
        {
            yield return new($"restricoes[{indice}]", new DomainError(
                ItemFormularioErrorCodes.RestricaoIncoerente,
                "A faixa numérica só se aplica ao campo numérico, o tamanho só ao de texto, as opções só ao de seleção, os municípios da UF só ao de município e a faixa de data só ao de data."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeCasasDecimaisDaFaixa(RestricaoValor restricao, int indice)
    {
        if (restricao is FaixaNumerica faixa
            && new[] { faixa.Minimo, faixa.Maximo }.Any(static limite => limite is { } v && Math.Round(v, FormaDoItem.CasasDecimaisDaFaixa) != v))
        {
            yield return new($"restricoes[{indice}]", new DomainError(
                RestricaoValorErrorCodes.LimitesIncoerentes,
                $"Os limites da faixa numérica têm no máximo {FormaDoItem.CasasDecimaisDaFaixa} casas decimais."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeRestricaoQueCitaOProprioFato(string fatoDoCampo, RestricaoValor restricao, int indice)
    {
        if (restricao.FatosCitados.Contains(fatoDoCampo, StringComparer.Ordinal))
        {
            yield return new($"restricoes[{indice}]", new DomainError(ItemFormularioErrorCodes.RegraAutorreferente, "A restrição cita o próprio fato."));
        }
    }

    /// <summary>
    /// O impedimento cabe no tipo do campo, cada cláusula dele cita a resposta do próprio campo, e a
    /// mensagem ao candidato é obrigatória e limitada.
    /// </summary>
    private IEnumerable<FieldError> RecusasDoImpedimento(string fatoDoCampo, TipoDeCampo? tipo) =>
        Impedimento is null
            ? []
            :
            [
                .. RecusasDoImpedimentoForaDoTipoDoCampo(tipo),
                .. RecusasDaCondicaoDoImpedimento(Impedimento, fatoDoCampo),
                .. RecusasDaMensagemDoImpedimento(Impedimento),
            ];

    private static IEnumerable<FieldError> RecusasDoImpedimentoForaDoTipoDoCampo(TipoDeCampo? tipo)
    {
        if (tipo is { } campo && !Impedimento.CabeNoCampo(campo))
        {
            yield return new("impedimento", new DomainError(
                ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo,
                "O impedimento só cabe em campo booleano, numérico, de seleção ou de município: texto, data e endereço não entram em regra."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaCondicaoDoImpedimento(Impedimento impedimento, string fatoDoCampo)
    {
        // Sem cláusula, o OU sobre elas nunca se cumpre, e o "toda cláusula cita o campo" aprovaria em vazio.
        if (impedimento.Quando.Clausulas.Count == 0)
        {
            yield return new("impedimento.quando", new DomainError(
                ItemFormularioErrorCodes.ImpedimentoSemCondicao, "O impedimento tem a condição sobre a resposta do campo."));
        }
        else if (impedimento.Quando.Clausulas.Any(clausula => !clausula.Condicoes.Any(c => string.Equals(c.Fato, fatoDoCampo, StringComparison.Ordinal))))
        {
            yield return new("impedimento.quando", new DomainError(
                ItemFormularioErrorCodes.ImpedimentoSemOProprioCampo,
                "Cada cláusula do impedimento cita a resposta do próprio campo; o bloqueio só por resposta anterior é da exibição."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaMensagemDoImpedimento(Impedimento impedimento)
    {
        if (FormaDoItem.TextoOpcional(impedimento.Mensagem) is not { Length: <= FormaDoItem.MensagemDoImpedimentoMaxLength } mensagem
            || mensagem.Contains('\0', StringComparison.Ordinal))
        {
            yield return new("impedimento.mensagem", new DomainError(
                ItemFormularioErrorCodes.ImpedimentoMensagemInvalida,
                $"A mensagem que explica o impedimento ao candidato é obrigatória e tem no máximo {FormaDoItem.MensagemDoImpedimentoMaxLength} caracteres."));
        }
    }
}

namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras do campo sobre as respostas anteriores (UNI-REQ-0145): quando aparece, quando é
/// obrigatório, que valores aceita e que resposta impede a inscrição. O impedimento cita também a
/// resposta do próprio campo; as demais regras, só respostas anteriores.
/// </summary>
public sealed record RegrasDoCampo
{
    public RegrasDoCampo(
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes,
        Impedimento? impedimento = null)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);

        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Restricoes = [.. restricoes];
        Impedimento = impedimento;
    }

    /// <summary>Quando o campo aparece; nulo quando aparece sempre.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }

    /// <summary>As restrições sobre o valor respondido, na ordem em que foram dadas.</summary>
    public IReadOnlyList<RestricaoValor> Restricoes { get; }

    public Impedimento? Impedimento { get; }

    /// <summary>
    /// Os fatos de que o campo depende: os citados pela exibição, pela obrigatoriedade, pelas restrições
    /// e pelo impedimento, sem repetição. O impedimento cita a resposta do próprio campo, que não é
    /// dependência: só as respostas anteriores entram.
    /// </summary>
    public IReadOnlyCollection<string> FatosCitadosPor(string fatoDoCampo) =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat((Impedimento?.FatosCitados ?? []).Where(f => !string.Equals(f, fatoDoCampo, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>As mesmas regras, com as restrições na ordem do tipo, a ordem em que o campo as guarda.</summary>
    public RegrasDoCampo ComRestricoesNaOrdemDoTipo() => new(Exibicao, Obrigatoriedade, [.. Restricoes.OrderBy(static r => r.Tipo)], Impedimento);

    /// <summary>
    /// Nenhuma regra, além do impedimento, cita o próprio fato, as restrições cabem no tipo do campo
    /// e o impedimento é coerente com ele. A autorreferência é ciclo de comprimento um, recusada aqui
    /// sem precisar conhecer os outros campos.
    /// </summary>
    public IEnumerable<FieldError> ConferirNoCampo(string fatoDoCampo, TipoRenderizacao tipo) =>
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
            yield return new("precondicao", new DomainError(ItemFormularioErrorCodes.RegraAutorreferente, "A exibição cita o próprio fato."));
        }
    }

    private IEnumerable<FieldError> RecusasDaObrigatoriedade(string fatoDoCampo)
    {
        if (Obrigatoriedade.FatosCitados.Contains(fatoDoCampo, StringComparer.Ordinal))
        {
            yield return new("predicadoObrigatoriedade", new DomainError(
                ItemFormularioErrorCodes.RegraAutorreferente, "A obrigatoriedade cita o próprio fato."));
        }
    }

    /// <summary>
    /// As restrições cabem no tipo do campo — faixa só no numérico, tamanho só no de texto, opções
    /// só no de seleção, municípios da UF só no de município, onde são obrigatórios, faixa de data só
    /// no de data —, não se repetem por tipo, não citam o próprio fato, e os limites da faixa cabem
    /// nas casas decimais do edital.
    /// </summary>
    private IEnumerable<FieldError> RecusasDasRestricoes(string fatoDoCampo, TipoRenderizacao tipo) =>
    [
        .. RecusasDoConjuntoDeRestricoes(tipo),
        .. Restricoes.SelectMany((restricao, indice) => RecusasDaRestricao(fatoDoCampo, tipo, restricao, indice)),
    ];

    private IEnumerable<FieldError> RecusasDoConjuntoDeRestricoes(TipoRenderizacao tipo)
    {
        if (RestricoesDeValor.TipoRepetido(Restricoes) is { } repetido)
        {
            yield return new("restricoes", repetido);
        }

        if (tipo == TipoRenderizacao.Municipio && !Restricoes.Any(static r => r is MunicipiosDaUf))
        {
            yield return new("restricoes", new DomainError(
                ItemFormularioErrorCodes.MunicipioSemUf, "O campo de município cita a UF respondida antes, pelos municípios da UF."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaRestricao(string fatoDoCampo, TipoRenderizacao tipo, RestricaoValor restricao, int indice) =>
    [
        .. RecusasDeRestricaoForaDoTipoDoCampo(tipo, restricao, indice),
        .. RecusasDeCasasDecimaisDaFaixa(restricao, indice),
        .. RecusasDeRestricaoQueCitaOProprioFato(fatoDoCampo, restricao, indice),
    ];

    private static IEnumerable<FieldError> RecusasDeRestricaoForaDoTipoDoCampo(TipoRenderizacao tipo, RestricaoValor restricao, int indice)
    {
        if (!FormaDoItem.RestricaoCabeNoCampo(restricao.Tipo, tipo))
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
    private IEnumerable<FieldError> RecusasDoImpedimento(string fatoDoCampo, TipoRenderizacao tipo) =>
        Impedimento is null
            ? []
            :
            [
                .. RecusasDoImpedimentoForaDoTipoDoCampo(tipo),
                .. RecusasDaCondicaoDoImpedimento(Impedimento, fatoDoCampo),
                .. RecusasDaMensagemDoImpedimento(Impedimento),
            ];

    private static IEnumerable<FieldError> RecusasDoImpedimentoForaDoTipoDoCampo(TipoRenderizacao tipo)
    {
        if (!Impedimento.CabeNoCampo(tipo))
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

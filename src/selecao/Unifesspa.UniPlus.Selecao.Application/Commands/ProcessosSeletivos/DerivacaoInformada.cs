namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A derivação de um fato do pedido, conferida com as recusas acumuladas (ADR-0125), em três grupos que
/// saem nesta ordem: a forma (da configuração e de cada regra), a semântica (o fato alvo e o predicado de cada
/// regra, contra o vocabulário e a oferta do processo) e a coerência da configuração montada com o domínio do fato.
/// Cada grupo corta só o que depende do dado recusado: o predicado mal formado corta a semântica da própria
/// regra, e a configuração com recusa de forma ou de semântica não é montada. Os campos vêm relativos à
/// configuração: <c>codigoFato</c>, <c>regras</c> e <c>regras[i]</c> com o campo da regra.
/// </summary>
internal sealed class DerivacaoInformada
{
    private readonly ConfiguracaoDerivacaoInput _entrada;
    private readonly List<RegraDeDerivacaoInformada> _regras;
    private readonly bool _fatoAlvoBemFormado;
    private readonly List<FieldError> _recusasDeForma;

    private DerivacaoInformada(ConfiguracaoDerivacaoInput entrada)
    {
        _entrada = entrada;
        _regras = [.. entrada.Regras.Select(RegraDeDerivacaoInformada.De)];
        _recusasDeForma = [.. ConfiguracaoDerivacaoFato.ValidarFormaBasica(entrada.CodigoFato, [.. entrada.Regras.Select(static r => (r.Ordem, r.Contribui))])];
        bool daOferta = entrada.CodigoFato is { } codigo && DerivacoesDaOferta.Deriva(codigo);
        if (daOferta)
        {
            _recusasDeForma.Add(new("codigoFato", DerivacoesDaOferta.RecusaDeConfiguracao(entrada.CodigoFato)));
        }

        _fatoAlvoBemFormado = !string.IsNullOrWhiteSpace(entrada.CodigoFato) && !daOferta;
        _recusasDeForma.AddRange(_regras.SelectMany(static (regra, i) => regra.RecusasDeForma.Select(erro => erro.NoCaminhoDe($"regras[{i}]"))));
    }

    public static DerivacaoInformada De(ConfiguracaoDerivacaoInput entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        return new DerivacaoInformada(entrada);
    }

    /// <summary>
    /// Confere a derivação contra o vocabulário do candidato e a oferta do processo, e a monta quando nada foi
    /// recusado.
    /// </summary>
    public ResolucaoDaDerivacao Resolver(ContextoDoCatalogo contexto, IReadOnlySet<string> universo)
    {
        List<FieldError> semantica = [.. RecusasDoFatoAlvo(contexto.Fatos), .. RecusasDoPredicadoDasRegras(contexto, universo)];
        if (_recusasDeForma.Count > 0 || semantica.Count > 0)
        {
            return new(_recusasDeForma, semantica, [], null);
        }

        Result<ConfiguracaoDerivacaoFato> montada = Montar(contexto.Fatos[_entrada.CodigoFato], contexto.DominiosDinamicos);
        return montada.IsSuccess
            ? new(_recusasDeForma, semantica, [], montada.Value)
            : new(_recusasDeForma, semantica, [new FieldError("regras", montada.Error!)], null);
    }

    // O alvo tem de existir e ser derivado com o binding da própria regra de derivação
    // (REGRA_DE_CALCULO:{codigo}); um fato declarado, ou derivado por outro mecanismo, não tem regras configuráveis.
    private IEnumerable<FieldError> RecusasDoFatoAlvo(IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        if (!_fatoAlvoBemFormado)
        {
            return [];
        }

        return !catalogo.TryGetValue(_entrada.CodigoFato, out FatoCandidatoView? alvo)
            ? [new FieldError("codigoFato", new DomainError(
                DerivabilidadeDeFato.FatoDesconhecido,
                "O fato não pertence ao vocabulário de fatos do candidato."))]
            : RecusaDeAlvoNaoDerivavel(alvo);
    }

    private static IEnumerable<FieldError> RecusaDeAlvoNaoDerivavel(FatoCandidatoView alvo) =>
        DerivabilidadeDeFato.EhAlvoDeDerivacao(alvo)
            ? []
            : [new FieldError("codigoFato", new DomainError(
                DerivabilidadeDeFato.FatoNaoDerivavel,
                "O fato não é um alvo de derivação — só um fato categórico ou booleano "
                + "derivado com binding de regra de derivação pode ter regras configuradas."))];

    private IEnumerable<FieldError> RecusasDoPredicadoDasRegras(ContextoDoCatalogo contexto, IReadOnlySet<string> universo) =>
        _regras.SelectMany((regra, i) => regra.RecusasDeSemantica(contexto.Vocabulario, universo, contexto.DominiosDinamicos)
            .Select(erro => erro.NoCaminhoDe($"regras[{i}]")));

    private Result<ConfiguracaoDerivacaoFato> Montar(FatoCandidatoView alvo, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        List<RegraDerivacaoConfigurada> regras = [];
        foreach (RegraDeDerivacaoInformada informada in _regras)
        {
            Result<RegraDerivacaoConfigurada> regra = informada.Montar();
            if (regra.IsFailure)
            {
                return Result<ConfiguracaoDerivacaoFato>.Failure(regra.Error!);
            }

            regras.Add(regra.Value!);
        }

        Result<ConfiguracaoDerivacaoFato> configuracao = ConfiguracaoDerivacaoFato.Criar(_entrada.CodigoFato, regras);
        return configuracao.IsFailure ? configuracao : ConferirComODominioDoFato(configuracao.Value!, alvo, dominiosDinamicos);
    }

    // O domínio do fato no catálogo decide a forma das regras, inclusive na fonte que o servidor não enumera: inferir
    // o booleano só das regras deixaria um categórico sem código virar booleano. Todo código contribuído pertence aos
    // valores que o fato tem no processo, pela fonte (ADR-0136); o valor desativado do catálogo pertence ao domínio e
    // é recusado adiante, como vínculo novo, e a fonte que o servidor não enumera (Geo) não tem conferência aqui.
    private static Result<ConfiguracaoDerivacaoFato> ConferirComODominioDoFato(
        ConfiguracaoDerivacaoFato configuracao, FatoCandidatoView alvo, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        bool alvoBooleano = alvo.DominioValor == TipoDoDado.SimOuNao;
        if (configuracao.Booleano != alvoBooleano)
        {
            return Result<ConfiguracaoDerivacaoFato>.Failure(RecusaDeContribuicaoIncoerente(alvoBooleano));
        }

        Result<DerivacaoDoFato>? dominio = VocabularioDeFatos.DominioDeContribuicao(alvo, dominiosDinamicos) is { } valores
            ? configuracao.ParaDerivacaoDoFato(valores)
            : null;
        return dominio is { IsFailure: true }
            ? Result<ConfiguracaoDerivacaoFato>.Failure(dominio.Error!)
            : Result<ConfiguracaoDerivacaoFato>.Success(configuracao);
    }

    private static DomainError RecusaDeContribuicaoIncoerente(bool alvoBooleano) => new(
        DerivabilidadeDeFato.ContribuicaoIncoerenteComODominio,
        alvoBooleano
            ? "O fato é booleano: as regras não contribuem código, e a ativa torna o derivado verdadeiro."
            : "O fato é categórico: toda regra contribui um código do domínio do fato.");
}

/// <summary>
/// O resultado de conferir a derivação de um fato: as recusas de forma, de semântica e de coerência com o domínio do
/// fato, e a configuração, presente só quando não houve recusa alguma.
/// </summary>
internal sealed record ResolucaoDaDerivacao(
    IReadOnlyList<FieldError> RecusasDeForma,
    IReadOnlyList<FieldError> RecusasDeSemantica,
    IReadOnlyList<FieldError> RecusasDeCoerencia,
    ConfiguracaoDerivacaoFato? Configuracao);

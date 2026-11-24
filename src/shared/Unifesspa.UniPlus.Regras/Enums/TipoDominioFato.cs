namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Domínio de valor de um fato do candidato, na forma que
/// <see cref="Regras.Services.PredicadoDnfValidador"/> consegue validar
/// genericamente. Deliberadamente mais estreito que o
/// <c>Dominio</c> do catálogo <c>dados_do_candidato</c> (ADR-0111, três
/// tokens: <c>OPCAO</c>/<c>SIM_OU_NAO</c>/<c>NUMERICO</c>): um fato
/// categórico de <b>escopo-processo</b> (<c>ValoresDominio</c> nulo — ex.:
/// <c>MODALIDADE</c>, <c>CONDICAO_ATENDIMENTO</c>) não é representável aqui.
/// A validação desse domínio dinâmico é responsabilidade do consumidor.
/// </summary>
public enum TipoDominioFato
{
    /// <summary>Escalar sim/não — só aceita o operador <see cref="Operador.Igual"/>.</summary>
    Booleano = 1,

    /// <summary>Escalar inteiro — aceita <see cref="Operador.Igual"/>, <see cref="Operador.MaiorIgual"/> e <see cref="Operador.MenorIgual"/>; nunca <see cref="Operador.AlgumDe"/>.</summary>
    Numerico = 2,

    /// <summary>Categórico com domínio estático enumerado (<c>ValoresDominio</c> preenchido) — aceita <see cref="Operador.Igual"/> e <see cref="Operador.AlgumDe"/>.</summary>
    CategoricoEstatico = 3,

    /// <summary>
    /// Categórico de <b>escopo-processo</b> (<c>ValoresDominio</c> nulo — ex.:
    /// <c>MODALIDADE</c>, <c>CONDICAO_ATENDIMENTO</c>), multivalorado — aceita
    /// <see cref="Operador.Igual"/> (pertinência) e <see cref="Operador.AlgumDe"/> (interseção). O
    /// domínio válido não vem de <c>ValoresDominio</c> (sempre nulo aqui) — vem de um
    /// domínio dinâmico fornecido pelo chamador, derivado da oferta do
    /// próprio processo (modalidades selecionadas, condições de atendimento ofertadas).
    /// </summary>
    CategoricoDinamico = 4,
}

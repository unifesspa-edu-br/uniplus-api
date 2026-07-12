namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// Traduz as violações de guard rail de banco da tabela
/// <c>versoes_configuracao</c> (ADR-0104) para <see cref="DomainError"/>
/// nomeado — que o mapper de contrato aflora como 422 (ADR-0102). O nome da
/// constraint vem de <see cref="UniqueConstraintViolation.GetViolatedConstraint"/>,
/// que já cobre <c>UNIQUE</c> (23505) e <c>CHECK</c> (23514) — e os triggers
/// de sucessão levantam <c>check_violation</c> declarando a constraint, para
/// caírem no mesmo caminho.
/// </summary>
/// <remarks>
/// Estas violações não substituem as invariantes de domínio (que recusam antes,
/// em memória): elas fecham a janela da corrida check-then-act entre duas
/// publicações concorrentes, e a do <c>INSERT</c> cru fora do agregado. Um
/// erro daqui é a prova de que a garantia é do banco, não da disciplina do
/// caller.
/// </remarks>
internal static class VersaoConfiguracaoConstraintViolation
{
    private const string ProcessoNumeroConstraint = "ux_versoes_configuracao_processo_numero";
    private const string AtoCriadorConstraint = "ux_versoes_configuracao_ato_criador";
    private const string NumeracaoContiguaConstraint = "ck_versoes_configuracao_numeracao_contigua";
    private const string CadeiaConstraint = "ck_versoes_configuracao_cadeia";
    private const string VigenciaMonotonicaConstraint = "ck_versoes_configuracao_vigencia_monotonica";
    private const string ContratoAberturaConstraint = "ck_versoes_configuracao_contrato_abertura";
    private const string NaoAutorretificaConstraint = "ck_versoes_configuracao_nao_autorretifica";

    /// <summary>
    /// <see cref="DomainError"/> correspondente à constraint violada, ou
    /// <see langword="null"/> quando a constraint não é desta tabela — caso em
    /// que o caller deve propagar a exceção original.
    /// </summary>
    public static DomainError? Traduzir(string? constraint) => constraint switch
    {
        ProcessoNumeroConstraint => new DomainError(
            "VersaoConfiguracao.NumeroDuplicado",
            "Outra publicação concorrente já criou esta versão da configuração — tente novamente."),

        AtoCriadorConstraint => new DomainError(
            "VersaoConfiguracao.AtoCriadorJaCriouVersao",
            "O ato informado já criou uma versão da configuração — um ato congela no máximo uma vez."),

        NumeracaoContiguaConstraint => new DomainError(
            "VersaoConfiguracao.NumeracaoComBuraco",
            "A numeração das versões da configuração é contígua — não há como criar uma versão sem a anterior."),

        CadeiaConstraint => new DomainError(
            "VersaoConfiguracao.CadeiaQuebrada",
            "O ato criador desta versão não retifica o ato criador da versão anterior — a cadeia de configuração do certame é única."),

        VigenciaMonotonicaConstraint => new DomainError(
            "VersaoConfiguracao.VigenciaRegressiva",
            "A vigência de uma versão não pode preceder a da versão anterior — é ela que ordena as versões."),

        ContratoAberturaConstraint => new DomainError(
            "VersaoConfiguracao.ContratoAberturaInvalido",
            "A versão 1 não retifica ato algum; toda versão seguinte retifica — o contrato não admite estado intermediário."),

        NaoAutorretificaConstraint => new DomainError(
            "VersaoConfiguracao.AtoCriadorRepetido",
            "Um ato não retifica a si mesmo — a cadeia de atos que congelam a configuração é linear."),

        _ => null,
    };
}

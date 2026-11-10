namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros da revisão das permissões reclassificadas que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class ReclassificacaoErros
{
    public static DomainError NaoEncontrada() => new(
        ReclassificacaoErrorCodes.NaoEncontrada,
        "A reclassificação não foi encontrada.");

    public static DomainError Superada() => new(
        ReclassificacaoErrorCodes.Superada,
        "Uma reclassificação mais recente da mesma permissão no perfil substituiu esta: recarregue a fila.");

    public static DomainError EtapaJaDecidida() => new(
        ReclassificacaoErrorCodes.EtapaJaDecidida,
        "A etapa já foi decidida para a concessão.");

    public static DomainError ConcessaoForaDaReclassificacao() => new(
        ReclassificacaoErrorCodes.ConcessaoForaDaReclassificacao,
        "A concessão não está suspensa por esta reclassificação ou foi revogada.");

    public static DomainError EtapaNaoExigida() => new(
        ReclassificacaoErrorCodes.EtapaNaoExigida,
        "A etapa não é exigida para a concessão: a base dela não mudou no que a etapa decide.");

    public static DomainError DecisorEBeneficiario() => new(
        ReclassificacaoErrorCodes.DecisorEBeneficiario,
        "Ninguém decide a própria concessão.");

    public static DomainError MesmoDecisorNasDuasEtapas() => new(
        ReclassificacaoErrorCodes.MesmoDecisorNasDuasEtapas,
        "A mesma pessoa não decide as duas etapas da reclassificação de uma concessão.");

    public static DomainError DocumentoNaoLido() => new(
        ReclassificacaoErrorCodes.DocumentoNaoLido,
        "Leia o documento instituidor de cada versão em uso antes de decidir.");

    public static DomainError EscopoNaoComporta() => new(
        ReclassificacaoErrorCodes.EscopoNaoComporta,
        "O escopo da concessão não comporta o novo alcance da permissão: o caminho é um pedido novo.");

    public static DomainError MotivoInvalido(int maximo) => new(
        ReclassificacaoErrorCodes.MotivoInvalido,
        $"O motivo é obrigatório ao manter a suspensão e deve ter até {maximo} caracteres.");

    public static DomainError SemPermissaoNaEtapa() => new(
        ReclassificacaoErrorCodes.SemPermissaoNaEtapa,
        "Acesso negado: a operação exige a permissão da etapa.");

    public static DomainError EtapaInvalida() => new(
        ReclassificacaoErrorCodes.EtapaInvalida,
        "Informe a etapa: gestao_de_acessos ou protecao_de_dados.");

    public static DomainError ResultadoInvalido() => new(
        ReclassificacaoErrorCodes.ResultadoInvalido,
        "Informe o resultado: liberada ou mantida.");

    public static DomainError AlvoInvalido() => new(
        ReclassificacaoErrorCodes.AlvoInvalido,
        "Informe as concessões da decisão ou marque todas, sem misturar os dois.");
}

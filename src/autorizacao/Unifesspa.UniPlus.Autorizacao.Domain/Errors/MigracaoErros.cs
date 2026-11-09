namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros da janela de migração que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class MigracaoErros
{
    public static DomainError JanelaNaoEncontrada() => new(
        MigracaoErrorCodes.JanelaNaoEncontrada,
        "O perfil não tem janela de migração aberta.");

    public static DomainError JanelaEncerrada() => new(
        MigracaoErrorCodes.JanelaEncerrada,
        "A janela de migração já terminou: não restava concessão pendente.");

    public static DomainError JanelaEmEncerramento() => new(
        MigracaoErrorCodes.JanelaEmEncerramento,
        "A janela de migração terminou agora e está sendo encerrada: repita a operação.");

    public static DomainError PrazoEncerrado() => new(
        MigracaoErrorCodes.PrazoEncerrado,
        "O prazo de 72 horas da janela de migração terminou: as concessões não decididas foram encerradas. Recarregue.");

    public static DomainError LoteJaEnviado() => new(
        MigracaoErrorCodes.LoteJaEnviado,
        "O lote da gestão de acessos já foi enviado para esta janela.");

    public static DomainError LoteIncompleto() => new(
        MigracaoErrorCodes.LoteIncompleto,
        "O lote precisa decidir todas as concessões da versão anterior, menos as do próprio gestor.");

    public static DomainError LoteInvalido() => new(
        MigracaoErrorCodes.LoteInvalido,
        "A lista traz decisão inválida, concessão repetida ou concessão que não é do perfil nem aguarda esta etapa.");

    public static DomainError LoteNaoEnviado() => new(
        MigracaoErrorCodes.LoteNaoEnviado,
        "O lote da gestão de acessos ainda não foi enviado, ou a concessão não consta dele.");

    public static DomainError DecisaoDaPropriaConcessao() => new(
        MigracaoErrorCodes.DecisaoDaPropriaConcessao,
        "Ninguém decide a própria concessão.");

    public static DomainError MesmaPessoaNasDuasEtapas() => new(
        MigracaoErrorCodes.MesmaPessoaNasDuasEtapas,
        "A mesma pessoa não decide as duas etapas da migração de uma concessão.");

    public static DomainError ConcessaoForaDaEtapa() => new(
        MigracaoErrorCodes.ConcessaoForaDaEtapa,
        "A concessão não aguarda a decisão desta etapa.");

    public static DomainError ConcessaoJaDecidida() => new(
        MigracaoErrorCodes.ConcessaoJaDecidida,
        "A concessão já foi decidida na janela de migração.");

    public static DomainError ExclusaoNaoConfirmada() => new(
        MigracaoErrorCodes.ExclusaoNaoConfirmada,
        "Recusar revoga a concessão: confirme a exclusão.");

    public static DomainError MotivoInvalido(int maximo) => new(
        MigracaoErrorCodes.MotivoInvalido,
        $"O motivo é obrigatório e deve ter até {maximo} caracteres.");

    public static DomainError SemPermissaoNaEtapa() => new(
        MigracaoErrorCodes.SemPermissaoNaEtapa,
        "Acesso negado: a operação exige a permissão da etapa.");

    public static DomainError ConflitoDeConcorrencia() => new(
        MigracaoErrorCodes.ConflitoDeConcorrencia,
        "A janela de migração foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}

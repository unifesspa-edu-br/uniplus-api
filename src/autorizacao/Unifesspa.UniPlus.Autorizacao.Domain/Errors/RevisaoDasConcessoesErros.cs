namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros da revisão das concessões que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class RevisaoDasConcessoesErros
{
    public static DomainError RevisaoNaoEncontrada() => new(
        RevisaoDasConcessoesErrorCodes.RevisaoNaoEncontrada,
        "O perfil não tem revisão das concessões aberta.");

    public static DomainError RevisaoEncerrada() => new(
        RevisaoDasConcessoesErrorCodes.RevisaoEncerrada,
        "A revisão das concessões já terminou: não restava concessão pendente.");

    public static DomainError RevisaoEmEncerramento() => new(
        RevisaoDasConcessoesErrorCodes.RevisaoEmEncerramento,
        "A revisão das concessões terminou agora e está sendo encerrada: repita a operação.");

    public static DomainError PrazoEncerrado() => new(
        RevisaoDasConcessoesErrorCodes.PrazoEncerrado,
        "O prazo de 72 horas da revisão das concessões terminou: as concessões não decididas foram encerradas. Recarregue.");

    public static DomainError DecisoesJaEnviadas() => new(
        RevisaoDasConcessoesErrorCodes.DecisoesJaEnviadas,
        "As decisões da gestão de acessos já foram enviadas para esta revisão.");

    public static DomainError DecisoesIncompletas() => new(
        RevisaoDasConcessoesErrorCodes.DecisoesIncompletas,
        "O envio precisa decidir todas as concessões da versão anterior, menos as do próprio gestor.");

    public static DomainError DecisoesInvalidas() => new(
        RevisaoDasConcessoesErrorCodes.DecisoesInvalidas,
        "A lista traz decisão inválida, concessão repetida ou concessão que não é do perfil nem aguarda esta etapa.");

    public static DomainError DecisoesNaoEnviadas() => new(
        RevisaoDasConcessoesErrorCodes.DecisoesNaoEnviadas,
        "As decisões da gestão de acessos ainda não foram enviadas, ou a concessão não consta do envio.");

    public static DomainError DecisaoDaPropriaConcessao() => new(
        RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao,
        "Ninguém decide a própria concessão.");

    public static DomainError MesmaPessoaNasDuasEtapas() => new(
        RevisaoDasConcessoesErrorCodes.MesmaPessoaNasDuasEtapas,
        "A mesma pessoa não decide as duas etapas da revisão de uma concessão.");

    public static DomainError ConcessaoForaDaEtapa() => new(
        RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa,
        "A concessão não aguarda a decisão desta etapa.");

    public static DomainError ConcessaoJaDecidida() => new(
        RevisaoDasConcessoesErrorCodes.ConcessaoJaDecidida,
        "A concessão já foi decidida na revisão das concessões.");

    public static DomainError ExclusaoNaoConfirmada() => new(
        RevisaoDasConcessoesErrorCodes.ExclusaoNaoConfirmada,
        "Recusar revoga a concessão: confirme a exclusão.");

    public static DomainError MotivoInvalido(int maximo) => new(
        RevisaoDasConcessoesErrorCodes.MotivoInvalido,
        $"O motivo é obrigatório e deve ter até {maximo} caracteres.");

    public static DomainError SemPermissaoNaEtapa() => new(
        RevisaoDasConcessoesErrorCodes.SemPermissaoNaEtapa,
        "Acesso negado: a operação exige a permissão da etapa.");

    public static DomainError PedidoEmPerfilEmMigracao() => new(
        PedidoErrorCodes.PerfilEmRevisaoDasConcessoes,
        "O perfil está em revisão das concessões e não recebe pedido novo até o fim da revisão.");
}

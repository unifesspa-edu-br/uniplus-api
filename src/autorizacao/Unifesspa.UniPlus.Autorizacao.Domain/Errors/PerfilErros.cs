namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros do perfil que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class PerfilErros
{
    public static DomainError GovernancaImutavel() => new(
        PerfilErrorCodes.GovernancaImutavel,
        "Perfil de governança é de sistema e não pode ser alterado, reativado nem desativado.");

    public static DomainError Inativo() => new(
        PerfilErrorCodes.Inativo,
        "O perfil de acesso está desativado e não aceita versão nem documento novos: reative-o antes.");

    public static DomainError NomeDuplicado() => new(
        PerfilErrorCodes.NomeDuplicado,
        "Já existe um perfil de acesso com o nome informado.");

    public static DomainError VersaoPendenteExistente() => new(
        PerfilErrorCodes.VersaoPendenteExistente,
        "O perfil já tem uma versão pendente: cancele-a ou espere o documento dela ser verificado.");

    public static DomainError VersaoNaoEncontrada() => new(
        PerfilErrorCodes.VersaoNaoEncontrada,
        "Versão do perfil de acesso não encontrada.");

    public static DomainError VersaoNaoPendente() => new(
        PerfilErrorCodes.VersaoNaoPendente,
        "A versão do perfil não está pendente.");

    public static DomainError DocumentoNaoAguardaEnvio() => new(
        PerfilErrorCodes.DocumentoNaoAguardaEnvio,
        "O documento da versão não está aguardando o envio do PDF.");

    public static DomainError DocumentoNaoRecusado() => new(
        PerfilErrorCodes.DocumentoNaoRecusado,
        "Só se envia outro documento quando o da versão foi recusado pela verificação.");

    public static DomainError DocumentoNaoVerificado() => new(
        PerfilErrorCodes.DocumentoNaoVerificado,
        "O documento da versão ainda não foi verificado e não pode ser lido.");

    public static DomainError AlcanceAlterado() => new(
        PerfilErrorCodes.AlcanceAlterado,
        "O alcance de um perfil não muda entre versões: crie outro perfil e conceda-o por pedido.");

    public static DomainError AmpliacaoExigeProtecaoDeDados() => new(
        PerfilErrorCodes.AmpliacaoExigeProtecaoDeDados,
        "Acrescentar permissão pessoal ou sensível a um perfil com concessões exige a aprovação da proteção de dados: crie outro perfil e conceda-o por pedido.");

    public static DomainError AcrescimoComConcessaoAtiva() => new(
        PerfilErrorCodes.AcrescimoComConcessaoAtiva,
        "Acrescentar permissão a um perfil com concessões ainda não é possível: crie outro perfil e conceda-o por pedido.");

    public static DomainError SemVersaoEmVigor() => new(
        PerfilErrorCodes.SemVersaoEmVigor,
        "O perfil não tem versão em vigor: não há permissão a remover.");

    public static DomainError RemocaoInvalida() => new(
        PerfilErrorCodes.RemocaoInvalida,
        "Informe ao menos uma permissão da versão em vigor para remover e, se informar a categoria, uma das previstas.");

    public static DomainError MotivoInvalido(int maximo) => new(
        PerfilErrorCodes.MotivoInvalido,
        $"O motivo é obrigatório e deve ter até {maximo} caracteres.");

    public static DomainError VersaoExpirada() => new(
        PerfilErrorCodes.VersaoExpirada,
        "A versão pendente passou do prazo de 15 dias e expirou sem entrar em vigor: crie outra.");

    public static DomainError EtapaInvalida() => new(
        PerfilErrorCodes.EtapaInvalida,
        "A etapa informada não é a que a versão aguarda agora.");

    public static DomainError EtapaJaDecidida() => new(
        PerfilErrorCodes.EtapaJaDecidida,
        "A etapa já foi decidida para esta versão.");

    public static DomainError AprovacoesDescartadas() => new(
        PerfilErrorCodes.AprovacoesDescartadas,
        "As aprovações exigidas pela versão mudaram e as já dadas foram descartadas: consulte a versão e decida de novo.");

    public static DomainError DecisorEAutor() => new(
        PerfilErrorCodes.DecisorEAutor,
        "Quem criou a versão não a decide.");

    public static DomainError MesmoDecisorNasDuasEtapas() => new(
        PerfilErrorCodes.MesmoDecisorNasDuasEtapas,
        "A mesma pessoa não decide as duas etapas da versão.");

    public static DomainError SemPermissaoNaEtapa() => new(
        PerfilErrorCodes.SemPermissaoNaEtapa,
        "Acesso negado: a operação exige a permissão da etapa.");

    public static DomainError PermissaoInexistente() => new(
        PerfilErrorCodes.PermissaoInexistente,
        "A versão reúne permissão que não consta mais do catálogo.");

    public static DomainError PermissaoReservada() => new(
        PerfilErrorCodes.PermissaoReservada,
        "A versão reúne permissão reservada aos perfis de governança.");

    public static DomainError JanelaDeMigracaoAberta() => new(
        PerfilErrorCodes.JanelaDeMigracaoAberta,
        "O perfil está em janela de migração e não aceita outra versão pendente até o fim dela.");
}

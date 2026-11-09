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
}

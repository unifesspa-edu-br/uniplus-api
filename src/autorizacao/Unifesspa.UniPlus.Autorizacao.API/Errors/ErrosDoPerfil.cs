namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: perfil de acesso.</summary>
internal static class ErrosDoPerfil
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Definicao(), .. Versoes(), .. Documento()];

    private static KeyValuePair<string, DomainErrorMapping>[] Definicao() =>
    [
        new(PerfilErrorCodes.NomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.nome_invalido", "Nome do perfil inválido")),
        new(PerfilErrorCodes.DescricaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.descricao_invalida", "Descrição do perfil inválida")),
        new(PerfilErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.sem_permissao", "O perfil precisa reunir ao menos uma permissão")),
        new(PerfilErrorCodes.PermissaoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_inexistente", "Permissão fora do catálogo")),
        new(PerfilErrorCodes.PermissaoReservada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_reservada", "Permissão reservada aos perfis de governança")),
        new(PerfilErrorCodes.AlcanceMisto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_misto", "O perfil não pode misturar permissões de unidade e institucionais")),
        new(PerfilErrorCodes.AlcanceAlterado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_alterado", "O alcance de um perfil com concessões não pode mudar")),
        new(PerfilErrorCodes.AmpliacaoExigeProtecaoDeDados, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.ampliacao_exige_protecao_de_dados", "Acrescentar permissão pessoal ou sensível a perfil com concessões exige a aprovação da proteção de dados")),
        new(PerfilErrorCodes.GovernancaImutavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.governanca_imutavel", "Perfil de governança não pode ser alterado")),
        new(PerfilErrorCodes.NomeDuplicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.nome_duplicado", "Já existe perfil com este nome")),
        new(PerfilErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.perfil.nao_encontrado", "Perfil de acesso não encontrado")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Versoes() =>
    [
        new(PerfilErrorCodes.VersaoPendenteExistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.versao_pendente_existente", "O perfil já tem uma versão pendente; cancele-a antes de criar outra")),
        new(PerfilErrorCodes.VersaoNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.perfil.versao_nao_encontrada", "Versão do perfil não encontrada")),
        new(PerfilErrorCodes.VersaoNaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.versao_nao_pendente", "A versão não está pendente")),
        new(PerfilErrorCodes.ProtocoloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.protocolo_invalido", "Protocolo do documento inválido")),
        new(PerfilErrorCodes.Inativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.inativo", "Perfil desativado não aceita versão nova")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Documento() =>
    [
        new(PerfilErrorCodes.DocumentoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.documento_invalido", "Documento da versão inválido")),
        new(PerfilErrorCodes.DocumentoNaoAguardaEnvio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.documento_nao_aguarda_envio", "O documento da versão não aguarda envio")),
        new(PerfilErrorCodes.DocumentoNaoRecusado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.documento_nao_recusado", "O documento da versão não foi recusado pela verificação")),
        new(PerfilErrorCodes.DocumentoNaoVerificado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.documento_nao_verificado", "O documento da versão ainda não foi verificado")),
        new(PerfilErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.perfil.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(PerfilErrorCodes.ConflitoDeConcorrencia),
    ];
}

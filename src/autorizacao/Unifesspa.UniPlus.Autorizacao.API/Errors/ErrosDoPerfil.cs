namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: perfil de acesso.</summary>
internal static class ErrosDoPerfil
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Definicao(), .. Versoes(), .. Documento(), .. Aprovacao(), .. Contencao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Definicao() =>
    [
        new(PerfilErrorCodes.NomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.nome_invalido", "Nome do perfil inválido")),
        new(PerfilErrorCodes.DescricaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.descricao_invalida", "Descrição do perfil inválida")),
        new(PerfilErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.sem_permissao", "O perfil precisa reunir ao menos uma permissão")),
        new(PerfilErrorCodes.PermissaoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_inexistente", "Permissão fora do catálogo")),
        new(PerfilErrorCodes.PermissaoReservada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_reservada", "Permissão reservada aos perfis de governança")),
        new(PerfilErrorCodes.AlcanceMisto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_misto", "O perfil não pode misturar permissões de unidade e institucionais")),
        new(PerfilErrorCodes.AlcanceAlterado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_alterado", "O alcance de um perfil com concessões não pode mudar")),
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
        new(PerfilErrorCodes.JanelaDeMigracaoAberta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.janela_de_migracao_aberta", "O perfil tem janela de migração aberta: nova versão só depois que ela terminar")),
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

    private static KeyValuePair<string, DomainErrorMapping>[] Aprovacao() =>
    [
        new(PerfilErrorCodes.SemVersaoEmVigor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.sem_versao_em_vigor", "O perfil não tem versão em vigor")),
        new(PerfilErrorCodes.RemocaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.remocao_invalida", "Permissões a remover inválidas")),
        new(PerfilErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.motivo_invalido", "Motivo ausente ou longo demais")),
        new(PerfilErrorCodes.VersaoExpirada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.versao_expirada", "O prazo da versão venceu", RecusaAposGravacao: true)),
        new(PerfilErrorCodes.EtapaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.etapa_invalida", "A etapa pedida não é a que a versão aguarda")),
        new(PerfilErrorCodes.EtapaJaDecidida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.etapa_ja_decidida", "A etapa da versão já foi decidida")),
        new(PerfilErrorCodes.AprovacoesDescartadas, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.aprovacoes_descartadas", "A mudança de classe descartou as aprovações; decida de novo", RecusaAposGravacao: true)),
        new(PerfilErrorCodes.DecisorEAutor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.decisor_e_autor", "Quem criou a versão não a decide")),
        new(PerfilErrorCodes.MesmoDecisorNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.mesmo_decisor_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(PerfilErrorCodes.SemPermissaoNaEtapa, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.perfil.sem_permissao_na_etapa", "Acesso negado: a operação exige a permissão da etapa")),
        new(PerfilErrorCodes.LeituraNegada, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Contencao() =>
    [
        new(PerfilErrorCodes.JaSuspenso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.ja_suspenso", "O perfil já está suspenso")),
        new(PerfilErrorCodes.NaoSuspenso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.nao_suspenso", "O perfil não está suspenso")),
        new(PerfilErrorCodes.RetomadaPorQuemSuspendeu, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.retomada_por_quem_suspendeu", "Quem suspendeu o perfil não decide a retomada")),
        new(PerfilErrorCodes.EtapaDaRetomadaJaDecidida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.etapa_da_retomada_ja_decidida", "A etapa da retomada já foi decidida")),
        new(PerfilErrorCodes.ReativacaoPorQuemDesativou, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.reativacao_por_quem_desativou", "Quem desativou o perfil não o reativa")),
    ];
}

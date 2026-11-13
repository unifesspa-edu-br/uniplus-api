namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: pedido de concessão.</summary>
internal static class ErrosDoPedido
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Cadastro(), .. Decisao(), .. Termo(), .. Consulta(), .. Acesso()];

    private static KeyValuePair<string, DomainErrorMapping>[] Cadastro() =>
    [
        new(PedidoErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.pedido.nao_encontrado", "Pedido de concessão não encontrado")),
        new(PedidoErrorCodes.PerfilEmJanelaDeMigracao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.perfil_em_janela_de_migracao", "O perfil está em janela de migração: o pedido só pode ser feito depois que ela terminar")),
        new(PedidoErrorCodes.PerfilInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.perfil_inativo", "Perfil de acesso desativado não aceita pedido")),
        new(PedidoErrorCodes.PerfilSemVersaoEmVigor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.perfil_sem_versao_em_vigor", "O perfil de acesso não tem versão em vigor")),
        new(PedidoErrorCodes.EscopoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.escopo_invalido", "Escopo incoerente ou não admitido pelo perfil")),
        new(PedidoErrorCodes.UnidadeInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.unidade_inexistente", "A unidade do escopo não existe")),
        new(PedidoErrorCodes.JustificativaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.justificativa_invalida", "Justificativa do pedido inválida")),
        new(PedidoErrorCodes.BeneficiarioInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.beneficiario_invalido", "Beneficiário do pedido inválido")),
        new(PedidoErrorCodes.SolicitanteNaoInstitucional, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.solicitante_nao_institucional", "Pedido por outra pessoa exige e-mail institucional verificado")),
        new(PedidoErrorCodes.BeneficiarioSemNome, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.beneficiario_sem_nome", "Beneficiário sem nome na sessão")),
        new(PedidoErrorCodes.PedidoDuplicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.pedido_duplicado", "Já existe pedido ativo igual para este beneficiário, perfil e escopo")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Decisao() =>
    [
        new(PedidoErrorCodes.EtapaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.etapa_invalida", "O pedido não aguarda a etapa deste aprovador")),
        new(PedidoErrorCodes.SituacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.situacao_invalida", "O pedido não está na situação que esta operação exige")),
        new(PedidoErrorCodes.DecisorEParte, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.decisor_e_parte", "Ninguém decide pedido em que é solicitante ou beneficiário")),
        new(PedidoErrorCodes.MesmoDecisorNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.mesmo_decisor_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(PedidoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.motivo_invalido", "Motivo da recusa inválido")),
        new(PedidoErrorCodes.JaTemConcessao, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.ja_tem_concessao", "O beneficiário já tem concessão ativa igual")),
        new(PedidoErrorCodes.PapeisIncompativeis, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.papeis_incompativeis", "O beneficiário já exerce papel incompatível")),
        new(PedidoErrorCodes.PrazoVencido, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.prazo_vencido", "O prazo da concessão venceu antes da aprovação", RecusaAposGravacao: true)),
        new(PedidoErrorCodes.ValidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.validade_invalida", "Fim da vigência inválido")),
        new(PedidoErrorCodes.PerfilAlterado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.perfil_alterado", "O perfil mudou depois da aprovação do gestor", RecusaAposGravacao: true)),
        new(PedidoErrorCodes.AnexoNaoVerificado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.anexo_nao_verificado", "O anexo ainda não foi verificado")),
        new(PedidoErrorCodes.AnexoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.anexo_invalido", "Anexo do pedido inválido")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Termo() =>
    [
        new(PedidoErrorCodes.SemTermoVigente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.sem_termo_vigente", "Não há termo de responsabilidade publicado")),
        new(PedidoErrorCodes.TermoDivergente, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.termo_divergente", "O termo apresentado não é o vigente")),
        new(PedidoErrorCodes.ApresentacaoDoTermoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.apresentacao_do_termo_invalida", "Versão do termo ausente ou indevida")),
        new(PedidoErrorCodes.AnexoNaoDeclarado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.anexo_nao_declarado", "O pedido não declarou documento")),
        new(PedidoErrorCodes.TermoExigido, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.termo_exigido", "O perfil passou a exigir o termo de responsabilidade", RecusaAposGravacao: true)),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Consulta() =>
    [
        new(PedidoErrorCodes.FilaInvalida, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.fila_invalida", "Fila de pedidos inválida")),
        new(PedidoErrorCodes.CursorInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.cursor_invalido", "Cursor de paginação dos pedidos inválido")),
        new(PedidoErrorCodes.FiltroDeSituacaoInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.filtro_de_situacao_invalido", "Situação do filtro de pedidos inválida")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Acesso() =>
    [
        new(PedidoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.autor_nao_identificado", "Autor da operação não identificado")),
        new(PedidoErrorCodes.NaoEOBeneficiario, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.nao_e_o_beneficiario", "Só o beneficiário do pedido o aceita")),
        new(PedidoErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),
        // O token não traz o que identifica o solicitante: não houve decisão de acesso a tomar, e a borda responde 401.
        new(PedidoErrorCodes.IdentidadeIncompleta, new DomainErrorMapping(StatusCodes.Status401Unauthorized, "uniplus.autorizacao.pedido.identidade_incompleta", "Identidade do solicitante incompleta")),
        MapeamentosComuns.Concorrencia(PedidoErrorCodes.ConflitoDeConcorrencia),
    ];
}

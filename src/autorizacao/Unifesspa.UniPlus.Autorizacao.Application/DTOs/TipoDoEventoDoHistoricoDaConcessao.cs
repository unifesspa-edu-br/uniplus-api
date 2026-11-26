namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que a trilha de uma concessão registra: o ato da própria concessão, da revisão, da migração, da providência e da origem que a gerou.</summary>
public enum TipoDoEventoDoHistoricoDaConcessao
{
    [CodigoDeContrato("CONCEDIDA", "Acesso concedido")]
    Concedida = 1,

    [CodigoDeContrato("VENCIDA", "Acesso vencido")]
    Vencida = 2,

    [CodigoDeContrato("REVOGADA", "Acesso revogado")]
    Revogada = 3,

    [CodigoDeContrato("REVOGADA_PELA_MIGRACAO", "Acesso revogado pela revisão das concessões")]
    RevogadaPelaMigracao = 4,

    [CodigoDeContrato("CONFIRMADA_NA_REVISAO", "Acesso mantido na revisão periódica")]
    ConfirmadaNaRevisao = 5,

    [CodigoDeContrato("REVOGADA_NA_REVISAO", "Acesso revogado na revisão periódica")]
    RevogadaNaRevisao = 6,

    [CodigoDeContrato("MIGRACAO_APROVADA", "Migração aprovada")]
    MigracaoAprovada = 7,

    [CodigoDeContrato("MIGRACAO_RECUSADA", "Migração recusada")]
    MigracaoRecusada = 8,

    [CodigoDeContrato("RETIDA_NA_MIGRACAO", "Concessão retida na migração")]
    RetidaNaMigracao = 9,

    [CodigoDeContrato("NAO_MIGRADA", "Concessão não migrada")]
    NaoMigrada = 10,

    [CodigoDeContrato("FORA_DA_LISTA_DA_MIGRACAO", "Retirada da lista da migração")]
    ForaDaListaDaMigracao = 11,

    [CodigoDeContrato("PROVIDENCIA_SOLICITADA", "Providência solicitada")]
    ProvidenciaSolicitada = 12,

    [CodigoDeContrato("PROVIDENCIA_RESPONDIDA", "Providência respondida")]
    ProvidenciaRespondida = 13,

    [CodigoDeContrato("PROVIDENCIA_ATENDIDA", "Providência atendida")]
    ProvidenciaAtendida = 14,

    [CodigoDeContrato("PEDIDO_CRIADO", "Pedido criado")]
    PedidoCriado = 15,

    [CodigoDeContrato("BENEFICIARIO_ACEITOU", "Beneficiário aceitou o pedido")]
    BeneficiarioAceitou = 16,

    [CodigoDeContrato("TERMO_ACEITO", "Termo de responsabilidade aceito")]
    TermoAceito = 17,

    [CodigoDeContrato("ANEXO_ENVIADO", "Documento enviado")]
    AnexoEnviado = 18,

    [CodigoDeContrato("ANEXO_VERIFICADO", "Documento verificado")]
    AnexoVerificado = 19,

    [CodigoDeContrato("ANEXO_RECUSADO", "Documento recusado na verificação")]
    AnexoRecusado = 20,

    [CodigoDeContrato("ANEXO_LIDO", "Documento consultado")]
    AnexoLido = 21,

    [CodigoDeContrato("PEDIDO_APROVADO_NA_ETAPA", "Aprovação")]
    PedidoAprovadoNaEtapa = 22,

    [CodigoDeContrato("PEDIDO_RECUSADO_NA_ETAPA", "Recusa")]
    PedidoRecusadoNaEtapa = 23,

    [CodigoDeContrato("PERFIL_ALTERADO_ENTRE_ETAPAS", "Perfil alterado entre as etapas")]
    PerfilAlteradoEntreEtapas = 24,

    [CodigoDeContrato("PRAZO_VENCIDO_ENTRE_ETAPAS", "Prazo vencido entre as etapas")]
    PrazoVencidoEntreEtapas = 25,

    [CodigoDeContrato("PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO", "Pedido devolvido por exigência do termo de responsabilidade")]
    PedidoDevolvidoPorTermoExigido = 26,

    [CodigoDeContrato("SEGUNDA_APROVACAO_DISPENSADA", "Segunda aprovação dispensada")]
    SegundaAprovacaoDispensada = 27,

    [CodigoDeContrato("PEDIDO_CANCELADO", "Pedido cancelado")]
    PedidoCancelado = 28,

    [CodigoDeContrato("INSTALACAO_INICIADA", "Instalação iniciada")]
    InstalacaoIniciada = 29,

    [CodigoDeContrato("DOCUMENTO_DE_DESIGNACAO_VERIFICADO", "Documento de designação verificado")]
    DocumentoDeDesignacaoVerificado = 30,

    [CodigoDeContrato("DOCUMENTO_DE_DESIGNACAO_RECUSADO", "Documento de designação recusado na verificação")]
    DocumentoDeDesignacaoRecusado = 31,

    [CodigoDeContrato("CODIGO_DE_CONFIRMACAO_ENVIADO", "Código de confirmação enviado")]
    CodigoDeConfirmacaoEnviado = 32,

    [CodigoDeContrato("CODIGO_DE_CONFIRMACAO_REENVIADO", "Código de confirmação reenviado")]
    CodigoDeConfirmacaoReenviado = 33,

    [CodigoDeContrato("TENTATIVAS_DE_CONFIRMACAO", "Tentativas de confirmação")]
    TentativasDeConfirmacao = 34,

    [CodigoDeContrato("INSTALACAO_CONFIRMADA", "Instalação confirmada")]
    InstalacaoConfirmada = 35,

    [CodigoDeContrato("INSTALACAO_CANCELADA", "Instalação cancelada")]
    InstalacaoCancelada = 36,

    [CodigoDeContrato("INSTALACAO_EXPIRADA", "Instalação expirada")]
    InstalacaoExpirada = 37,

    [CodigoDeContrato("INSTALACAO_SEM_ACEITE_DO_TERMO", "Instalação sem aceite do termo de responsabilidade")]
    InstalacaoSemAceiteDoTermo = 38,
}

namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDoEventoDoHistoricoDaConcessaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDoEventoDoHistoricoDaConcessao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(Esperados);

    private static readonly Dictionary<TipoDoEventoDoHistoricoDaConcessao, string> Esperados = new()
    {
        [TipoDoEventoDoHistoricoDaConcessao.Concedida] = "CONCEDIDA",
        [TipoDoEventoDoHistoricoDaConcessao.Vencida] = "VENCIDA",
        [TipoDoEventoDoHistoricoDaConcessao.Revogada] = "REVOGADA",
        [TipoDoEventoDoHistoricoDaConcessao.RevogadaPelaMigracao] = "REVOGADA_PELA_MIGRACAO",
        [TipoDoEventoDoHistoricoDaConcessao.ConfirmadaNaRevisao] = "CONFIRMADA_NA_REVISAO",
        [TipoDoEventoDoHistoricoDaConcessao.RevogadaNaRevisao] = "REVOGADA_NA_REVISAO",
        [TipoDoEventoDoHistoricoDaConcessao.MigracaoAprovada] = "MIGRACAO_APROVADA",
        [TipoDoEventoDoHistoricoDaConcessao.MigracaoRecusada] = "MIGRACAO_RECUSADA",
        [TipoDoEventoDoHistoricoDaConcessao.RetidaNaMigracao] = "RETIDA_NA_MIGRACAO",
        [TipoDoEventoDoHistoricoDaConcessao.NaoMigrada] = "NAO_MIGRADA",
        [TipoDoEventoDoHistoricoDaConcessao.ForaDaListaDaMigracao] = "FORA_DA_LISTA_DA_MIGRACAO",
        [TipoDoEventoDoHistoricoDaConcessao.ProvidenciaSolicitada] = "PROVIDENCIA_SOLICITADA",
        [TipoDoEventoDoHistoricoDaConcessao.ProvidenciaRespondida] = "PROVIDENCIA_RESPONDIDA",
        [TipoDoEventoDoHistoricoDaConcessao.ProvidenciaAtendida] = "PROVIDENCIA_ATENDIDA",
        [TipoDoEventoDoHistoricoDaConcessao.PedidoCriado] = "PEDIDO_CRIADO",
        [TipoDoEventoDoHistoricoDaConcessao.BeneficiarioAceitou] = "BENEFICIARIO_ACEITOU",
        [TipoDoEventoDoHistoricoDaConcessao.TermoAceito] = "TERMO_ACEITO",
        [TipoDoEventoDoHistoricoDaConcessao.AnexoEnviado] = "ANEXO_ENVIADO",
        [TipoDoEventoDoHistoricoDaConcessao.AnexoVerificado] = "ANEXO_VERIFICADO",
        [TipoDoEventoDoHistoricoDaConcessao.AnexoRecusado] = "ANEXO_RECUSADO",
        [TipoDoEventoDoHistoricoDaConcessao.AnexoLido] = "ANEXO_LIDO",
        [TipoDoEventoDoHistoricoDaConcessao.PedidoAprovadoNaEtapa] = "PEDIDO_APROVADO_NA_ETAPA",
        [TipoDoEventoDoHistoricoDaConcessao.PedidoRecusadoNaEtapa] = "PEDIDO_RECUSADO_NA_ETAPA",
        [TipoDoEventoDoHistoricoDaConcessao.PerfilAlteradoEntreEtapas] = "PERFIL_ALTERADO_ENTRE_ETAPAS",
        [TipoDoEventoDoHistoricoDaConcessao.PrazoVencidoEntreEtapas] = "PRAZO_VENCIDO_ENTRE_ETAPAS",
        [TipoDoEventoDoHistoricoDaConcessao.PedidoDevolvidoPorTermoExigido] = "PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO",
        [TipoDoEventoDoHistoricoDaConcessao.SegundaAprovacaoDispensada] = "SEGUNDA_APROVACAO_DISPENSADA",
        [TipoDoEventoDoHistoricoDaConcessao.PedidoCancelado] = "PEDIDO_CANCELADO",
        [TipoDoEventoDoHistoricoDaConcessao.InstalacaoIniciada] = "INSTALACAO_INICIADA",
        [TipoDoEventoDoHistoricoDaConcessao.DocumentoDeDesignacaoVerificado] = "DOCUMENTO_DE_DESIGNACAO_VERIFICADO",
        [TipoDoEventoDoHistoricoDaConcessao.DocumentoDeDesignacaoRecusado] = "DOCUMENTO_DE_DESIGNACAO_RECUSADO",
        [TipoDoEventoDoHistoricoDaConcessao.CodigoDeConfirmacaoEnviado] = "CODIGO_DE_CONFIRMACAO_ENVIADO",
        [TipoDoEventoDoHistoricoDaConcessao.CodigoDeConfirmacaoReenviado] = "CODIGO_DE_CONFIRMACAO_REENVIADO",
        [TipoDoEventoDoHistoricoDaConcessao.TentativasDeConfirmacao] = "TENTATIVAS_DE_CONFIRMACAO",
        [TipoDoEventoDoHistoricoDaConcessao.InstalacaoConfirmada] = "INSTALACAO_CONFIRMADA",
        [TipoDoEventoDoHistoricoDaConcessao.InstalacaoCancelada] = "INSTALACAO_CANCELADA",
        [TipoDoEventoDoHistoricoDaConcessao.InstalacaoExpirada] = "INSTALACAO_EXPIRADA",
        [TipoDoEventoDoHistoricoDaConcessao.InstalacaoSemAceiteDoTermo] = "INSTALACAO_SEM_ACEITE_DO_TERMO",
    };
}

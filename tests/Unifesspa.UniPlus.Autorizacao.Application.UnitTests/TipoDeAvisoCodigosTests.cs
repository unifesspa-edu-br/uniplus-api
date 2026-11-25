namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDeAvisoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDeAviso>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(Esperados);

    private static readonly Dictionary<TipoDeAviso, string> Esperados = new()
    {
        [TipoDeAviso.PermissoesRemovidas] = "PERMISSOES_REMOVIDAS",
        [TipoDeAviso.VersaoCanceladaPelaRemocao] = "VERSAO_CANCELADA_PELA_REMOCAO",
        [TipoDeAviso.AprovacoesDescartadas] = "APROVACOES_DESCARTADAS",
        [TipoDeAviso.RevisaoDasConcessoesAberta] = "REVISAO_DAS_CONCESSOES_ABERTA",
        [TipoDeAviso.RevisaoDasConcessoesFaltam48Horas] = "REVISAO_DAS_CONCESSOES_FALTAM_48_HORAS",
        [TipoDeAviso.RevisaoDasConcessoesFaltam24Horas] = "REVISAO_DAS_CONCESSOES_FALTAM_24_HORAS",
        [TipoDeAviso.ConcessaoRetidaNaMigracao] = "CONCESSAO_RETIDA_NA_MIGRACAO",
        [TipoDeAviso.PerfilSuspenso] = "PERFIL_SUSPENSO",
        [TipoDeAviso.PermissaoReclassificada] = "PERMISSAO_RECLASSIFICADA",
        [TipoDeAviso.EscopoDaPermissaoAlterado] = "ESCOPO_DA_PERMISSAO_ALTERADO",
        [TipoDeAviso.DivergenciaDaGovernanca] = "DIVERGENCIA_DA_GOVERNANCA",
        [TipoDeAviso.VersaoPendenteApta] = "VERSAO_PENDENTE_APTA",
        [TipoDeAviso.MigracoesAguardandoProtecaoDeDados] = "MIGRACOES_AGUARDANDO_PROTECAO_DE_DADOS",
        [TipoDeAviso.DecisoesEnviadasComRecusas] = "DECISOES_ENVIADAS_COM_RECUSAS",
        [TipoDeAviso.ConcessaoMigrada] = "CONCESSAO_MIGRADA",
        [TipoDeAviso.ConcessaoRevogada] = "CONCESSAO_REVOGADA",
        [TipoDeAviso.RevogacoesNaMigracao] = "REVOGACOES_NA_MIGRACAO",
        [TipoDeAviso.VersaoEncerrada] = "VERSAO_ENCERRADA",
        [TipoDeAviso.AdvertenciaDeFimDaMigracao] = "ADVERTENCIA_DE_FIM_DA_MIGRACAO",
        [TipoDeAviso.PedidoDevolvidoPelaRevisao] = "PEDIDO_DEVOLVIDO_PELA_REVISAO",
        [TipoDeAviso.PedidoDevolvidoPorTermoExigido] = "PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO",
        [TipoDeAviso.ProvidenciaSolicitada] = "PROVIDENCIA_SOLICITADA",
        [TipoDeAviso.ProvidenciaRespondida] = "PROVIDENCIA_RESPONDIDA",
        [TipoDeAviso.ProvidenciaAtendida] = "PROVIDENCIA_ATENDIDA",
        [TipoDeAviso.ConcessaoRevogadaNoAtendimento] = "CONCESSAO_REVOGADA_NO_ATENDIMENTO",
    };
}

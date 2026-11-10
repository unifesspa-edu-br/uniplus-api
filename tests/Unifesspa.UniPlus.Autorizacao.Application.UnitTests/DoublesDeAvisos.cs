namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Dependências neutras do <see cref="AvisosDoPedido"/> para os testes dos handlers que só precisam delas montadas.</summary>
internal static class DoublesDeAvisos
{
    public static AvisosDoPedido Avisos() => new(new SemTitulares(), new SemUnidade(), new LinkDeTeste(), TimeProvider.System);

    /// <summary>Os receptores do resultado da verificação, na ordem de consulta, com perfis e versões neutros.</summary>
    public static IReadOnlyList<IReceptorDoResultadoDaVerificacao> Receptores(
        IInstalacaoRepository instalacoes, IPedidoDeConcessaoRepository pedidos, IAutorizacaoUnitOfWork unidade) =>
    [
        new ReceptorDoDocumentoDaInstalacao(instalacoes, unidade, TimeProvider.System, NullLogger<ReceptorDoDocumentoDaInstalacao>.Instance),
        new ReceptorDoAnexoDoPedido(pedidos, new PerfilQualquer(), Avisos(), unidade, TimeProvider.System, NullLogger<ReceptorDoAnexoDoPedido>.Instance),
        new ReceptorDoDocumentoDaVersao(Versoes(), NullLogger<ReceptorDoDocumentoDaVersao>.Instance),
    ];

    /// <summary>O tratamento de versões do perfil sem perfil algum: nenhum arquivo dos testes é documento de versão.</summary>
    public static ResultadoDoDocumentoDaVersao Versoes() =>
        Versoes(new DoublesDePerfis.PerfisNaMemoria(), new DoublesDePerfis.UnidadeDeTrabalho(), TimeProvider.System);

    /// <summary>O tratamento de versões do perfil sobre os perfis dados, com a unidade de trabalho e o relógio dados.</summary>
    public static ResultadoDoDocumentoDaVersao Versoes(DoublesDePerfis.PerfisNaMemoria perfis, IAutorizacaoUnitOfWork unidade, TimeProvider relogio) =>
        new(
            perfis,
            new OperacaoSobreOPerfil(perfis, unidade, relogio),
            CenarioDaGovernancaDaVersao.AvisoDeEtapaApta(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), null, relogio));

    internal sealed class PerfilQualquer : IPerfilRepository
    {
        public Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Perfil?>(PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], new CatalogoLivre()));

        public Task<TrilhaDaContencao?> ObterTrilhaDaContencaoAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Perfil entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Perfil entity) => throw new InvalidOperationException();
        public void Remover(Perfil entity) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPediveisPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Guid?> ObterPerfilIdDoDocumentoAsync(Guid documentoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarLeituraAsync(LeituraDoDocumentoDaVersao leitura, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Perfil>> ListarComPendenteNoPrazoAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Guid>> ListarComPendenteVencidaAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Perfil>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class SemUnidade : ILeitorDeUnidade
    {
        public Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidadeView?>(null);
    }

    internal sealed class LinkDeTeste : ILinkDoPedido, ILinkDaGovernanca
    {
        public string Para(Guid pedidoId) => $"https://uniplus.test/configuracao/acessos/pedidos/{pedidoId}";

        public string DoPerfil(Guid perfilId) => $"https://uniplus.test/configuracao/perfis/{perfilId}";

        public string DaVersao(Guid perfilId, Guid versaoId) => $"{DoPerfil(perfilId)}/versoes/{versaoId}";

        public string DaJanela(Guid perfilId) => $"{DoPerfil(perfilId)}/janela";

        public string DasConcessoes() => "https://uniplus.test/configuracao/concessoes";

        public string DoNovoPedido() => "https://uniplus.test/configuracao/pedidos/novo";
    }

    internal static MetricasDaGovernancaDePerfis MetricasDeTeste { get; } = new(TimeProvider.System);

    internal sealed class SemTitulares : IConcessaoRepository
    {
        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TitularComEmail>>([]);

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterParaMigrarAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class CatalogoLivre : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => false;

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}

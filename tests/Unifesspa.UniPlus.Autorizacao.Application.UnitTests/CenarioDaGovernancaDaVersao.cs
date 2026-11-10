namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>Monta, com dublês em memória, os casos de uso da governança de versões que atuam em nome de uma pessoa.</summary>
internal sealed class CenarioDaGovernancaDaVersao
{
    public const string Gestao = "autorizacao:perfis:aprovar-funcional";
    public const string Protecao = "autorizacao:perfis:aprovar-protecao-de-dados";
    public const string Manter = "autorizacao:perfis:manter";

    public CenarioDaGovernancaDaVersao(
        Perfil perfil,
        Identidade? ator, string[] permitidas,
        TitularDaPermissao[]? titulares = null,
        TimeProvider? relogio = null,
        EstadoDasConcessoes? estado = null,
        Perfil[]? outrosPerfis = null)
    {
        TimeProvider tempo = relogio ?? new RelogioFixo(PerfisDeTeste.Instante.AddHours(1));
        EstadoDasConcessoes situacao = estado ?? new EstadoDasConcessoes();
        Concessoes = new(situacao.TemConcessao, titulares: []);
        JanelasNaMemoria janelas = new(situacao.JanelaAberta);
        ConcessoesNaMemoria concessoes = Concessoes;
        Perfis = new PerfisNaMemoria([perfil, .. outrosPerfis ?? []]);
        Unidade = new UnidadeDeTrabalho();
        Verificador = new VerificadorDeTeste(ator?.Sub ?? "sem-autor", permitidas);
        OperacaoSobreOPerfil operacao = new(Perfis, Unidade, tempo);
        AutoriaDaOperacao autoria = new(new Usuario(ator), tempo, concessoes);
        ConferenciaDoCatalogoNaVersao conferencia = new(new ReclassificacoesNaMemoria(), new CatalogoDaAplicacao([]), tempo);
        EfeitosDoDesfechoDaVersao efeitos = new(
            new EfeitosDaTrocaDeVigencia(
                concessoes,
                new PedidosAtivosNaMemoria(),
                new AvisosDoPedido(concessoes, new SemUnidade(), new LinkDeTeste(), tempo),
                AvisoDeDevolucao(Registro),
                JanelaDaTroca(janelas, Registro, tempo, titulares),
                tempo),
            new AvisosDaVersao(
                new EmissorDeAvisos(Registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
                new LinkDeTeste(),
                new CatalogoDaAplicacao([])),
            new CatalogoDaAplicacao([]),
            conferencia);
        Aprovacoes = new AprovacoesDaVersao(
            operacao, new LeitorDoContextoDaVersao(Perfis, janelas, tempo), efeitos, autoria, new CatalogoDeTeste(), conferencia);
        Cancelamento = new CancelamentoDaVersao(operacao, efeitos, autoria);
        Remocao = new RemocaoDePermissoes(operacao, efeitos, autoria, new CatalogoDeTeste());
        Ativacao = new AtivacaoDoPerfil(operacao, autoria, new CatalogoDeTeste());
        Contencao = new ContencaoPorSuspensao(
            operacao,
            autoria,
            new AvisosDaContencao(
                new EmissorDeAvisos(Registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
                new LinkDeTeste()),
            new CatalogoDeTeste());
        Propostas = new PropostasDeVersao(
            operacao, new LeitorDoContextoDaVersao(Perfis, janelas, tempo), efeitos, new EnvioDoDocumento(Armazenamento, tempo), autoria, new CatalogoDeTeste());
        Expiracao = new ExpiracaoDasVersoesPendentes(Perfis, new BarramentoDeTeste(operacao), tempo, NullLogger<ExpiracaoDasVersoesPendentes>.Instance);
    }

    public PerfisNaMemoria Perfis { get; }

    public ConcessoesNaMemoria Concessoes { get; }

    public UnidadeDeTrabalho Unidade { get; }

    public VerificadorDeTeste Verificador { get; }

    public RegistroDeAvisosNaMemoria Registro { get; } = new();

    /// <summary>Os efeitos da janela de migração sobre uma troca, sobre a janela em memória.</summary>
    public static EfeitosDaJanelaNaTroca JanelaDaTroca(
        JanelasNaMemoria janelas, RegistroDeAvisosNaMemoria registro, TimeProvider relogio, TitularDaPermissao[]? titulares = null) =>
        new(
            janelas,
            new LeitorDaMigracao(janelas, new CatalogoDeTeste(), relogio),
            new AvisosDaJanela(
                new EmissorDeAvisos(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
                new LinkDeTeste(),
                new CatalogoDaAplicacao([])));

    public AprovacoesDaVersao Aprovacoes { get; }

    public CancelamentoDaVersao Cancelamento { get; }

    public RemocaoDePermissoes Remocao { get; }

    public AtivacaoDoPerfil Ativacao { get; }

    public ContencaoPorSuspensao Contencao { get; }

    public PropostasDeVersao Propostas { get; }

    public Armazenamento Armazenamento { get; } = new();

    public ExpiracaoDasVersoesPendentes Expiracao { get; }

    /// <summary>Perfil com a versão pendente de documento já verificado: pronta para a decisão da gestão.</summary>
    public static Perfil PendenteVerificada(string[] permissoes)
    {
        Perfil perfil = PerfisDeTeste.Pendente("Análise de atendimento", "Analisa os pedidos de atendimento.", permissoes, new CatalogoDeTeste());
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag-de-teste", PerfisDeTeste.Instante));
        perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), PerfisDeTeste.Instante);

        return perfil;
    }

    private sealed class BarramentoDeTeste(OperacaoSobreOPerfil operacao) : ICommandBus
    {
        public async Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default) =>
            command is ExpirarVersaoPendenteDoPerfilCommand expirar
                ? (TResponse)(object)await ExpirarVersaoPendenteDoPerfilCommandHandler.Handle(expirar, operacao, ct)
                : throw new NotSupportedException();
    }

    public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    public static AvisoDePedidoDevolvido AvisoDeDevolucao(IRegistroDeAvisos registro) =>
        new(new EmissorDeAvisos(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance), new LinkDeTeste());

    public sealed class RegistroDeAvisosNaMemoria : IRegistroDeAvisos
    {
        private readonly HashSet<Guid> _ja = [];

        public Task<IReadOnlyCollection<Guid>> RegistrarAsync(
            string tipo, Guid fatoId, IReadOnlyList<Guid> mensagensIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([.. mensagensIds.Where(_ja.Add)]);
    }

    private sealed class TitularesDeTeste(TitularDaPermissao[] titulares) : ITitularesDaPermissao
    {
        public Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(
            string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TitularDaPermissao>>(titulares);
    }

}

/// <summary>O que o cenário simula das concessões: a janela de migração aberta e se quem decide tem concessão do perfil.</summary>
internal sealed record EstadoDasConcessoes(bool JanelaAberta = false, bool TemConcessao = false);

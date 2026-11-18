namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
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
        RevisoesNaMemoria revisoes = new(situacao.RevisaoAberta);
        Perfis = new PerfisNaMemoria([perfil, .. outrosPerfis ?? []]);
        Unidade = new UnidadeDeTrabalho();
        Verificador = new VerificadorDeTeste(ator?.ContaId ?? "sem-autor", permitidas);
        OperacaoSobreOPerfil operacao = new(Perfis, Unidade, tempo);
        AutoriaDaOperacao autoria = new(new Usuario(ator), tempo, Concessoes);
        ConferenciaDoCatalogoNaVersao conferencia = new(new ReclassificacoesNaMemoria(), new CatalogoDaAplicacao([]), tempo);
        EfeitosDoDesfechoDaVersao efeitos = Efeitos(Concessoes, revisoes, titulares, tempo, conferencia);
        MontarCasosDeUso(operacao, autoria, efeitos, conferencia, revisoes, titulares, tempo);
    }

    private EfeitosDoDesfechoDaVersao Efeitos(
        ConcessoesNaMemoria concessoes,
        RevisoesNaMemoria revisoes,
        TitularDaPermissao[]? titulares,
        TimeProvider tempo,
        ConferenciaDoCatalogoNaVersao conferencia) =>
        new(
            EfeitosDaTroca(concessoes, revisoes, titulares, tempo),
            new AvisosDaVersao(
                new EmissorDeAvisos(Registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
                new LinkDeTeste(),
                new CatalogoDaAplicacao([])),
            AvisoDeEtapaApta(Registro, titulares, tempo),
            new CatalogoDaAplicacao([]),
            conferencia);

    private EfeitosDaTrocaDeVigencia EfeitosDaTroca(
        ConcessoesNaMemoria concessoes, RevisoesNaMemoria revisoes, TitularDaPermissao[]? titulares, TimeProvider tempo) =>
        new(
            concessoes,
            RevisaoDaTroca(revisoes, Registro, tempo, titulares),
            AvisosDosEfeitos(Registro, titulares),
            new ReavaliacaoDosPedidosNaTroca(
                new PedidosAtivosNaMemoria(), new AvisosDoPedido(concessoes, new SemUnidade(), new LinkDeTeste(), tempo), AvisoDeDevolucao(Registro), tempo),
            tempo);

    [MemberNotNull(
        nameof(Aprovacoes), nameof(Cancelamento), nameof(Remocao), nameof(Ativacao), nameof(Contencao), nameof(Propostas), nameof(Expiracao))]
    private void MontarCasosDeUso(
        OperacaoSobreOPerfil operacao,
        AutoriaDaOperacao autoria,
        EfeitosDoDesfechoDaVersao efeitos,
        ConferenciaDoCatalogoNaVersao conferencia,
        RevisoesNaMemoria revisoes,
        TitularDaPermissao[]? titulares,
        TimeProvider tempo)
    {
        LeitorDoContextoDaVersao leitor = new(Perfis, revisoes, tempo);
        Aprovacoes = new AprovacoesDaVersao(operacao, leitor, efeitos, autoria, new CatalogoDeTeste(), conferencia);
        Cancelamento = new CancelamentoDaVersao(operacao, efeitos, autoria);
        Remocao = new RemocaoDePermissoes(operacao, efeitos, autoria, new CatalogoDeTeste());
        Ativacao = new AtivacaoDoPerfil(operacao, autoria, new CatalogoDeTeste());
        Contencao = new ContencaoPorSuspensao(operacao, autoria, AvisosDaContencaoDe(titulares), new CatalogoDeTeste());
        Propostas = new PropostasDeVersao(
            operacao, leitor, efeitos, new EnvioDoDocumento(new EnvioDoPdf(Armazenamento), tempo), autoria, new CatalogoDeTeste());
        Expiracao = new ExpiracaoDasVersoesPendentes(Perfis, new BarramentoDeTeste(operacao), tempo, NullLogger<ExpiracaoDasVersoesPendentes>.Instance);
    }

    private AvisosDaContencao AvisosDaContencaoDe(TitularDaPermissao[]? titulares) =>
        new(
            new EmissorDeAvisos(Registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
            new LinkDeTeste());

    public PerfisNaMemoria Perfis { get; }

    public ConcessoesNaMemoria Concessoes { get; }

    public UnidadeDeTrabalho Unidade { get; }

    public VerificadorDeTeste Verificador { get; }

    public RegistroDeAvisosNaMemoria Registro { get; } = new();

    /// <summary>Os efeitos da revisão das concessões sobre uma troca, sobre a revisão em memória.</summary>
    public static EfeitosDaRevisaoNaTroca RevisaoDaTroca(
        RevisoesNaMemoria revisoes, RegistroDeAvisosNaMemoria registro, TimeProvider relogio, TitularDaPermissao[]? titulares = null) =>
        new(
            revisoes,
            new LeitorDaMigracao(revisoes, new CatalogoDeTeste(), relogio),
            new AvisosDaRevisao(
                new EmissorDeAvisos(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
                new LinkDeTeste(),
                new CatalogoDaAplicacao([])),
            AvisosDosEfeitos(registro, titulares));

    /// <summary>Os avisos do que a revisão das concessões fez às concessões e à versão anterior, sobre o registro em memória.</summary>
    public static AvisosDosEfeitosDaRevisao AvisosDosEfeitos(RegistroDeAvisosNaMemoria registro, TitularDaPermissao[]? titulares = null) =>
        new(
            new EmissorDeAvisos(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]));

    public AprovacoesDaVersao Aprovacoes { get; private set; }

    public CancelamentoDaVersao Cancelamento { get; private set; }

    public RemocaoDePermissoes Remocao { get; private set; }

    public AtivacaoDoPerfil Ativacao { get; private set; }

    public ContencaoPorSuspensao Contencao { get; private set; }

    public PropostasDeVersao Propostas { get; private set; }

    public Armazenamento Armazenamento { get; } = new();

    public ExpiracaoDasVersoesPendentes Expiracao { get; private set; }

    /// <summary>Perfil com a versão pendente de documento já verificado: pronta para a decisão da gestão.</summary>
    public static Perfil PendenteVerificada(string[] permissoes)
    {
        Perfil perfil = PerfisDeTeste.Pendente("Análise de atendimento", "Analisa os pedidos de atendimento.", permissoes, new CatalogoDeTeste());
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(DocumentosDoPerfil.Confirmar(perfil, versao.Id, "etag-de-teste", PerfisDeTeste.Instante));
        DocumentosDoPerfil.RegistrarVerificado(perfil, documento.Id, new string('a', 64), PerfisDeTeste.Instante);

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

    public static AvisoDeEtapaApta AvisoDeEtapaApta(IRegistroDeAvisos registro, TitularDaPermissao[]? titulares, TimeProvider tempo) =>
        new(
            new EmissorDeAvisos(registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDeTeste(titulares ?? [])),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]),
            tempo);

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

/// <summary>O que o cenário simula das concessões: a revisão das concessões aberta e se quem decide tem concessão do perfil.</summary>
internal sealed record EstadoDasConcessoes(bool RevisaoAberta = false, bool TemConcessao = false);

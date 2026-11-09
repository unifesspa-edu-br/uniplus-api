namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
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
        Perfil perfil, Identidade? ator, string[] permitidas, TitularDaPermissao[]? titulares = null, TimeProvider? relogio = null)
    {
        TimeProvider tempo = relogio ?? new RelogioFixo(PerfisDeTeste.Instante.AddHours(1));
        ConcessoesNaMemoria concessoes = new(titulares: []);
        Perfis = new PerfisNaMemoria(perfil);
        Unidade = new UnidadeDeTrabalho();
        Verificador = new VerificadorDeTeste(ator?.Sub ?? "sem-autor", permitidas);
        OperacaoSobreOPerfil operacao = new(Perfis, Unidade);
        AutoriaDaOperacao autoria = new(new Usuario(ator), tempo, concessoes);
        EfeitosDoDesfechoDaVersao efeitos = new(
            new EfeitosDaTrocaDeVigencia(
                concessoes,
                new PedidosAtivosNaMemoria(),
                new AvisosDoPedido(concessoes, new SemUnidade(), new LinkDeTeste(), tempo),
                tempo),
            new AvisosDaVersao(
                new EmissorDeAvisos(Registro, NullLogger<EmissorDeAvisos>.Instance),
                new TitularesDeTeste(titulares ?? []),
                new LinkDaGovernancaDeTeste(),
                new CatalogoDaAplicacao([])),
            new CatalogoDaAplicacao([]));
        Aprovacoes = new AprovacoesDaVersao(
            operacao, new LeitorDoContextoDaVersao(Perfis, concessoes), efeitos, autoria, new CatalogoDeTeste());
        Cancelamento = new CancelamentoDaVersao(operacao, efeitos, autoria);
        Remocao = new RemocaoDePermissoes(operacao, efeitos, autoria);
        Expiracao = new ExpiracaoDasVersoesPendentes(Perfis, operacao, tempo, NullLogger<ExpiracaoDasVersoesPendentes>.Instance);
    }

    public PerfisNaMemoria Perfis { get; }

    public UnidadeDeTrabalho Unidade { get; }

    public VerificadorDeTeste Verificador { get; }

    public RegistroDeAvisosNaMemoria Registro { get; } = new();

    public AprovacoesDaVersao Aprovacoes { get; }

    public CancelamentoDaVersao Cancelamento { get; }

    public RemocaoDePermissoes Remocao { get; }

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

    public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

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

    private sealed class LinkDaGovernancaDeTeste : ILinkDaGovernanca
    {
        public string DoPerfil(Guid perfilId) => $"https://uniplus.test/configuracao/perfis/{perfilId}";

        public string DaVersao(Guid perfilId, Guid versaoId) => $"https://uniplus.test/configuracao/perfis/{perfilId}/versoes/{versaoId}";
    }
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As pessoas, os perfis e o catálogo comuns aos testes do pedido de concessão; os cenários se montam com <see cref="PedidoDeConcessaoBuilder"/>.</summary>
public abstract class PedidoDeConcessaoTestBase
{
    internal const string Emissor = "https://idp.test/realms/unifesspa";
    internal const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";

    internal static readonly DateTimeOffset Agora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    internal static readonly ICatalogoDePermissoesConsultavel Catalogo = new FakeCatalogo();
    internal static readonly IDominiosInstitucionais Dominios = new FakeDominios();
    private protected static readonly ContextoDaAprovacao Contexto = new(HaRepresentanteVigente: false);

    internal static readonly PessoaNoToken Betina = Pessoa("betina.neves", "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local");
    internal static readonly PessoaNoToken Leandro = Pessoa("leandro.viana", "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local");
    private protected static readonly PessoaNoToken Diogo = Pessoa("diogo.souza", "Diogo Bruno Filipe Souza", "diogo.bruno.souza@uniplus-test.local");
    private protected static readonly PessoaNoToken Isabella = Pessoa("isabella.figueiredo", "Isabella Priscila Figueiredo", "isabellapriscilafigueiredo@uniplus-test.local");
    private protected static readonly PessoaNoToken Marlene = Pessoa("marlene.martins", "Marlene Márcia Emily Martins", "marlenemarciamartins@uniplus-test.local");

    private protected static readonly PerfilAvaliado Interno = new(["selecao:processos:consultar"], "interno", false, null);
    private protected static readonly PerfilAvaliado Sensivel = new(["selecao:atendimento:analisar", "selecao:processos:consultar"], "sensivel", true, null);
    private protected static readonly PerfilAvaliado DeGestor = new(["autorizacao:perfis:manter"], "pessoal", true, TipoDeGovernanca.GestorDeAcessos);
    private protected static readonly PerfilAvaliado DeRepresentante = new(["autorizacao:concessoes:administrar"], "pessoal", true, TipoDeGovernanca.RepresentanteDoControlador);

    internal static EscopoDaConcessao EscopoDe(Perfil? perfil) =>
        perfil?.Governanca is null ? EscopoDaConcessao.DaUnidade(Guid.NewGuid()) : EscopoDaConcessao.DaInstituicao();

    internal static PessoaNoToken Pessoa(string sub, string nome, string email) =>
        new(new Identidade(Emissor, sub), nome, email, true);

    internal static Perfil PerfilComum() =>
        PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], Catalogo);

    internal static Perfil PerfilDeGovernanca(TipoDeGovernanca governanca = TipoDeGovernanca.GestorDeAcessos)
    {
        return PerfisDeTeste.DeGovernanca("Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], Catalogo, governanca);
    }

    private protected static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private protected static string[] Codigos<T>(Result<T> resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }

    private sealed class FakeDominios : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => string.Equals(dominio, "uniplus-test.local", StringComparison.OrdinalIgnoreCase);
    }
}

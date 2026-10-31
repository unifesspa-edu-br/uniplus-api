namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Profile;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PerfisConcedidosDoUsuarioTests
{
    private const string Emissor = "https://idp.exemplo/realms/unifesspa";
    private const string Sub = "leandro.viana";
    private const string Gestor = "Gestor de processo seletivo";

    private static readonly DateTimeOffset Agora = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ceps = Guid.CreateVersion7();
    private static readonly Guid Cga = Guid.CreateVersion7();
    private static readonly Guid Extinta = Guid.CreateVersion7();

    private readonly IConcessoesAtivasDoSujeito _concessoes = Substitute.For<IConcessoesAtivasDoSujeito>();
    private readonly IUnidadeReader _unidades = Substitute.For<IUnidadeReader>();

    public PerfisConcedidosDoUsuarioTests()
    {
        _unidades.ObterPorIdAsync(Ceps, Arg.Any<CancellationToken>()).Returns(Unidade(Ceps, "CEPS", "Centro de Processos Seletivos"));
        _unidades.ObterPorIdAsync(Cga, Arg.Any<CancellationToken>()).Returns(Unidade(Cga, "CGA", "Centro de Gestão Acadêmica"));
    }

    [Fact]
    public async Task Pessoa_SemConcessao_RecebeListaVazia()
    {
        Dado();

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcessaoDeUnidade_TrazUnidadeNomeEPrazo()
    {
        ConcessaoAtiva concessao = Concessao(Gestor, Ceps, validaAte: Agora.AddDays(1));
        Dado(concessao);

        PerfilConcedidoResponse perfil = (await ListarAsync()).Should().ContainSingle().Subject;

        perfil.Should().Be(new PerfilConcedidoResponse(
            concessao.ConcessaoId, concessao.PerfilId, Gestor, "unidade", Ceps, "CEPS", "Centro de Processos Seletivos", Agora.AddDays(1)));
    }

    [Fact]
    public async Task ConcessaoInstitucional_NaoTrazUnidadeNemPrazo()
    {
        Dado(Concessao("Consulta institucional", null));

        PerfilConcedidoResponse perfil = (await ListarAsync()).Should().ContainSingle().Subject;

        perfil.EscopoTipo.Should().Be("instituicao");
        (perfil.UnidadeId, perfil.UnidadeSigla, perfil.UnidadeNome, perfil.ValidaAte).Should().Be((null, null, null, null));
    }

    [Fact]
    public async Task ConcessaoVencida_NaoAparece_InclusiveNoInstanteExato()
    {
        Dado(
            Concessao("Vencida ontem", Ceps, validaAte: Agora.AddDays(-1)),
            Concessao("Vence agora", Ceps, validaAte: Agora),
            Concessao("Vence depois", Ceps, validaAte: Agora.AddTicks(1)));

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Select(p => p.Nome).Should().Equal("Vence depois");
    }

    [Fact]
    public async Task ConsultaAsConcessoesDaPessoaDoToken()
    {
        Dado();

        await ListarAsync();

        await _concessoes.Received(1).ListarAsync(
            Arg.Is<UsuarioRef>(p => p.Emissor == Emissor && p.Subject == Sub),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnidadeNaoEncontrada_ContinuaNaListaComSiglaENomeNulos()
    {
        Dado(Concessao(Gestor, Extinta));

        PerfilConcedidoResponse perfil = (await ListarAsync()).Should().ContainSingle().Subject;

        (perfil.UnidadeId, perfil.UnidadeSigla, perfil.UnidadeNome).Should().Be((Extinta, null, null));
        perfil.EscopoTipo.Should().Be("unidade");
    }

    [Fact]
    public async Task MesmoPerfilEmVariosEscopos_InstituicaoPrimeiroDepoisUnidadesPelaSigla()
    {
        ConcessaoAtiva cga = Concessao(Gestor, Cga);
        ConcessaoAtiva institucional = Concessao(Gestor, null);
        ConcessaoAtiva ceps = Concessao(Gestor, Ceps);
        Dado(cga, institucional, ceps);

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Select(p => p.ConcessaoId).Should().Equal(institucional.ConcessaoId, ceps.ConcessaoId, cga.ConcessaoId);
    }

    [Fact]
    public async Task UnidadeSemSigla_VaiParaOFimDoSeuPerfil()
    {
        ConcessaoAtiva extinta = Concessao(Gestor, Extinta);
        ConcessaoAtiva cga = Concessao(Gestor, Cga);
        Dado(extinta, cga);

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Select(p => p.ConcessaoId).Should().Equal(cga.ConcessaoId, extinta.ConcessaoId);
    }

    [Fact]
    public async Task OrdenaPeloNomeDoPerfilAntesDoEscopo()
    {
        Dado(Concessao("Gestor", null), Concessao("Consulta", Cga), Concessao("Ágil", Ceps));

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Select(p => p.Nome).Should().Equal("Ágil", "Consulta", "Gestor");
    }

    [Fact]
    public async Task EmpateNoPerfilEnoEscopo_MantemADataDaConcessaoDaPorta()
    {
        ConcessaoAtiva primeira = Concessao(Gestor, Ceps);
        ConcessaoAtiva segunda = Concessao(Gestor, Ceps);
        Dado(primeira, segunda);

        IReadOnlyList<PerfilConcedidoResponse> perfis = await ListarAsync();

        perfis.Select(p => p.ConcessaoId).Should().Equal(primeira.ConcessaoId, segunda.ConcessaoId);
    }

    [Theory]
    [InlineData(null, Sub)]
    [InlineData("", Sub)]
    [InlineData(Emissor, null)]
    [InlineData(Emissor, " ")]
    public async Task TokenSemEmissorOuSubject_Falha(string? emissor, string? sub)
    {
        Result<IReadOnlyList<PerfilConcedidoResponse>> resultado = await PerfisConcedidosDoUsuario.ListarAsync(
            emissor, sub, _concessoes, _unidades, Agora, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        await _concessoes.DidNotReceiveWithAnyArgs().ListarAsync(default!, default);
    }

    private static UnidadeView Unidade(Guid id, string sigla, string nome) =>
        new(id, sigla, sigla.ToLowerInvariant(), nome, null, "Centro", true, null);

    private static ConcessaoAtiva Concessao(string perfil, Guid? unidadeId, DateTimeOffset? validaAte = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), perfil, unidadeId, unidadeId is null, validaAte, []);

    private void Dado(params ConcessaoAtiva[] concessoes) =>
        _concessoes.ListarAsync(Arg.Any<UsuarioRef>(), Arg.Any<CancellationToken>()).Returns(concessoes);

    private async Task<IReadOnlyList<PerfilConcedidoResponse>> ListarAsync()
    {
        Result<IReadOnlyList<PerfilConcedidoResponse>> resultado = await PerfisConcedidosDoUsuario.ListarAsync(
            Emissor, Sub, _concessoes, _unidades, Agora, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }
}

namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class AutorizacaoDaMigracaoTests
{
    private static readonly Guid PerfilId = Guid.NewGuid();

    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();

    [Fact(DisplayName = "Sem autor identificado, a migração recusa pelo autor, não registra e não lê a concessão")]
    public async Task Autorizar_SemAutor_RecusaSemRegistrarNemLerAConcessao()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        Result<AutoriaDaVersao> resultado = await Autorizacao(PerfisDeTeste.Gestor).AutorizarAsync(PerfilId, EtapaDaVersao.GestaoDeAcessos, verificador, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
        await _concessoes.DidNotReceiveWithAnyArgs().TemConcessaoAtivaDoPerfilAsync(default!, default, default, default);
    }

    [Fact(DisplayName = "Sem a permissão da etapa, a migração recusa, registra a negada da etapa e não lê a concessão")]
    public async Task Autorizar_SemPermissaoDaEtapa_RecusaERegistraANegada()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        Result<AutoriaDaVersao> resultado = await Autorizacao(PerfisDeTeste.Gestor).AutorizarAsync(PerfilId, EtapaDaVersao.ProtecaoDeDados, verificador, CancellationToken.None);

        resultado.Error!.Code.Should().Be(RevisaoDasConcessoesErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(entrada => entrada.Permissao == Protecao).Which.Resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _concessoes.DidNotReceiveWithAnyArgs().TemConcessaoAtivaDoPerfilAsync(default!, default, default, default);
    }

    [Fact(DisplayName = "Com a permissão da etapa, a migração devolve a autoria com a concessão do perfil")]
    public async Task Autorizar_ComPermissaoDaEtapa_DevolveAAutoria()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);
        _concessoes.TemConcessaoAtivaDoPerfilAsync(default!, default, default, default).ReturnsForAnyArgs(true);

        Result<AutoriaDaVersao> resultado = await Autorizacao(PerfisDeTeste.Gestor).AutorizarAsync(PerfilId, EtapaDaVersao.GestaoDeAcessos, verificador, CancellationToken.None);

        resultado.Value!.Pessoa.Should().Be(PerfisDeTeste.Gestor);
        resultado.Value.TemConcessao.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(entrada => entrada.Permissao == Gestao).Which.Resultado.Should().Be(ResultadoDoAcesso.Permitido);
    }

    [Fact(DisplayName = "Com a permissão da etapa e o token sem identidade, a migração registra a decisão e recusa pelo autor")]
    public async Task Autorizar_PermitidoSemIdentidadeNoToken_RegistraERecusaPeloAutor()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        Result<AutoriaDaVersao> resultado = await Autorizacao(null).AutorizarAsync(PerfilId, EtapaDaVersao.GestaoDeAcessos, verificador, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(entrada => entrada.Permissao == Gestao);
    }

    private AutorizacaoDaMigracao Autorizacao(Identidade? pessoa) =>
        new(new AutoriaDaOperacao(new Usuario(pessoa), TimeProvider.System, _concessoes));
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.AcessoPelaBanca;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services.AcessoPelaBanca;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A revelação pelo caminho da banca decide, nesta ordem, a avaliação da inscrição (com a designação), a classe mascarada
/// e a permissão de revelar. Consultar a avaliação ou a classe sozinhas nunca revela, e o que não se confere contra a
/// versão vigente responde o 404 do campo sem decidir nada.
/// </summary>
public sealed class AcessoARevelacaoPelaBancaTests
{
    private const string CodigoDoCampoNaoEncontrado = "Inscricao.CampoNaoEncontrado";

    private static readonly string Avaliacoes = PermissaoSelecao.AvaliacoesConsultar.Codigo;
    private static readonly string Classe = PermissaoSelecao.DadosIdentificadoresConsultar.Codigo;
    private static readonly string Revelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;

    private readonly InscricaoAdministrativaDeTeste _cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
        .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, null, formato: "CPF");

    private readonly Guid _banca = Guid.CreateVersion7();
    private readonly Guid _etapa = Guid.CreateVersion7();
    private readonly UnidadeDaFase _fase = new(Guid.CreateVersion7(), Guid.CreateVersion7());

    [Fact(DisplayName = "Designada à banca, com as três permissões, revela; a avaliação é decidida na unidade da fase, e não na dona")]
    public async Task Designada_ComAsTresPermissoes_Revela()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar)
            .DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick);

        resultado.IsSuccess.Should().BeTrue();
        await ExigiuAAvaliacaoNaUnidadeDaFaseAsync(erick);
        await erick.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == Revelar),
            Arg.Is<ResourceContext?>(r => r!.FatoCodigo == "CPF" && r.UnidadeProprietariaId == _fase.UnidadeId),
            Arg.Any<CancellationToken>());
    }

    private Task<ResultadoDoAcesso> ExigiuAAvaliacaoNaUnidadeDaFaseAsync(DecisoesDeAcessoDeTeste persona) =>
        persona.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == Avaliacoes),
            Arg.Is<ResourceContext?>(r => r!.ResourceTipo == ResourceTiposSelecao.AvaliacaoDaInscricao
                && r.UnidadeProprietariaId == _fase.UnidadeId
                && r.UnidadeProprietariaId != DecisoesDeAcessoDeTeste.UnidadeDona
                && r.Banca!.BancaId == _banca && r.Banca.EtapaId == _etapa && r.Banca.FaseId == _fase.FaseId
                && r.InscricaoId == _cenario.InscricaoId),
            Arg.Any<CancellationToken>());

    [Fact(DisplayName = "Com as três permissões e sem a designação à banca, responde 404 e não decide a classe nem revelar")]
    public async Task SemDesignacao_NaoEncontrado_ENaoDecideRevelar()
    {
        DecisoesDeAcessoDeTeste kevin = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar)
            .DesignadaA(Guid.CreateVersion7());

        Result<VinculoDaInscricao> resultado = await Decidir(kevin);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await kevin.NaoExigiuAsync(Revelar);
        await kevin.NaoAvaliouNadaAsync();
    }

    [Fact(DisplayName = "Consultar a avaliação sozinha não revela: a classe mascarada é recusada e só a negativa dela é registrada")]
    public async Task SoAvaliacao_NaoRevela()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        erick.DecisoesRegistradas().Should().ContainSingle().Which.Should().BeOfType<DecisaoNegada>()
            .Subject.Registro.Permissao.Should().Be(Classe);
        await erick.NaoExigiuAsync(Revelar);
    }

    [Fact(DisplayName = "Consultar a avaliação e a classe, sem revelar, responde 404")]
    public async Task SemRevelar_NaoEncontrado()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar).DesignadaA(_banca);

        (await Decidir(erick)).Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
    }

    [Fact(DisplayName = "Só a classe e revelar, sem a avaliação, responde 404 antes de avaliar a classe")]
    public async Task SemAvaliacao_NaoAvaliaAClasse()
    {
        DecisoesDeAcessoDeTeste kevin = Persona(PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(kevin);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await kevin.NaoAvaliouNadaAsync();
        await kevin.NaoExigiuAsync(Revelar);
    }

    [Fact(DisplayName = "A etapa de outra fase responde 404 sem decidir nada")]
    public async Task EtapaDeOutraFase_NaoEncontrado()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick, new UnidadeDaFase(Guid.CreateVersion7(), _fase.UnidadeId));

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoExigiuNadaAsync();
    }

    [Fact(DisplayName = "A etapa que não se resolve na versão vigente responde 404 sem decidir nada")]
    public async Task EtapaSemResolver_NaoEncontrado()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick, faseDaEtapa: null);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoExigiuNadaAsync();
    }

    [Fact(DisplayName = "A inscrição de outro processo ou inexistente responde 404 sem decidir nada")]
    public async Task InscricaoInexistente_NaoEncontrado()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick, inscricaoId: Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoExigiuNadaAsync();
    }

    [Fact(DisplayName = "A banca com identificador vazio responde 404 sem decidir nada")]
    public async Task BancaVazia_NaoEncontrado()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar).DesignadaA(_banca);

        Result<VinculoDaInscricao> resultado = await Decidir(erick, bancaId: Guid.Empty);

        resultado.Error!.Code.Should().Be(CodigoDoCampoNaoEncontrado);
        await erick.NaoExigiuNadaAsync();
    }

    [Fact(DisplayName = "A capacidade de revelar pela banca usa as mesmas decisões e não grava nada")]
    public async Task Capacidade_SemRegistro()
    {
        DecisoesDeAcessoDeTeste erick = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar)
            .DesignadaA(_banca);

        bool pode = await Servico(erick, _fase).PodeAsync(Alvo(_banca), CancellationToken.None);

        pode.Should().BeTrue();
        await erick.NaoExigiuNadaAsync();
        await erick.NaoRegistrouNadaAsync();
    }

    [Fact(DisplayName = "Fora da designação, ou sem uma das permissões, a capacidade é falsa")]
    public async Task Capacidade_ForaDaDesignacaoOuSemPermissao_Falsa()
    {
        DecisoesDeAcessoDeTeste foraDaBanca = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);
        DecisoesDeAcessoDeTeste semRevelar = Persona(PermissaoSelecao.AvaliacoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar).DesignadaA(_banca);

        (await Servico(foraDaBanca, _fase).PodeAsync(Alvo(_banca), CancellationToken.None)).Should().BeFalse();
        (await Servico(semRevelar, _fase).PodeAsync(Alvo(_banca), CancellationToken.None)).Should().BeFalse();
        await foraDaBanca.NaoRegistrouNadaAsync();
    }

    private static DecisoesDeAcessoDeTeste Persona(params PermissaoSelecao[] concedidas) => new("erick.araujo", concedidas);

    private AlvoDaRevelacaoPelaBanca Alvo(Guid bancaId, Guid? inscricaoId = null) =>
        new(_cenario.ProcessoId, _fase.FaseId, _etapa, bancaId, inscricaoId ?? _cenario.InscricaoId, "CPF", null);

    private AcessoARevelacaoPelaBanca Servico(DecisoesDeAcessoDeTeste persona, UnidadeDaFase? faseDaEtapa = null)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = _cenario.Montar();
        IUnidadeDaFaseNaVersaoVigente fases = Substitute.For<IUnidadeDaFaseNaVersaoVigente>();
        fases.DaEtapaAsync(_cenario.ProcessoId, _etapa, Arg.Any<CancellationToken>()).Returns(faseDaEtapa);

        return new AcessoARevelacaoPelaBanca(persona.Verificador, mundo.Processos, mundo.Inscricoes, fases);
    }

    private Task<Result<VinculoDaInscricao>> Decidir(
        DecisoesDeAcessoDeTeste persona, UnidadeDaFase? faseDaEtapa, Guid? inscricaoId = null, Guid? bancaId = null) =>
        Servico(persona, faseDaEtapa).DecidirAsync(Alvo(bancaId ?? _banca, inscricaoId), CancellationToken.None);

    private Task<Result<VinculoDaInscricao>> Decidir(DecisoesDeAcessoDeTeste persona, Guid? inscricaoId = null, Guid? bancaId = null) =>
        Decidir(persona, _fase, inscricaoId, bancaId);
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.FormulariosDaInscricaoDeTeste;

/// <summary>
/// Os formulários apresentados da versão que a inscrição fixou ao iniciar: resolve
/// pelo <c>VersaoFormularioId</c> gravado, nunca pela divulgação atual — a mesma projeção de
/// <see cref="ObterFormularioApresentadoVersaoEReidratacaoTests"/>, só a resolução
/// da versão é diferente.
/// </summary>
public sealed class ObterFormulariosDaInscricaoQueryHandlerTests
{
    private static readonly IReadOnlyList<FormularioDaVersao> DoisObrigatoriosEUmFacultativo =
    [
        new(FormularioDeTeste.CodigoDaInscricao, Obrigatorio: true),
        new("AAA_SOLICITACAO_DE_ISENCAO", Obrigatorio: false),
        new("COMPLEMENTO_DA_INSCRICAO", Obrigatorio: true),
    ];

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        PortasDaInscricaoDeTeste portas = new(ProcessoId);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, userContext);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await portas.Inscricoes.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        PortasDaInscricaoDeTeste portas = new(ProcessoId);
        portas.ComInscricao(cpf, null);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Devolve o formulário da versão divulgada ao rascunho")]
    public async Task Handle_RascunhoDevolveFormularioDaVersaoDivulgada()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao();
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormularioApresentadoDoProcessoDto formulario = resultado.Value!.Should().ContainSingle().Subject;
        formulario.Codigo.Should().Be(FormularioDeTeste.CodigoDaInscricao);
        formulario.Obrigatorio.Should().BeTrue();
    }

    [Fact(DisplayName = "O formulário da inscrição do candidato não traz a lista de comprovação: os documentos dele vêm da avaliação")]
    public async Task Handle_FormularioComBlocoDeComprovacao_NaoTrazALista()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(comBlocoDeComprovacao: true, comExigenciaCondicional: true);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().ContainSingle().Which.ComprovacaoDocumental.Should().BeNull("a lista do certame não diz o que se aplica ao candidato");
    }

    [Fact(DisplayName = "Inscrição enviada na versão fixada sem formulário de inscrição é defeito, não ausência: a versão já foi provada ao iniciar")]
    public async Task Handle_VersaoFixadaSemFormularioDeInscricao_LancaDefeito()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, _) = MontarVersaoComFormularioDeInscricao(
            formularios: [new FormularioDaVersao("SOLICITACAO_DE_ISENCAO", Obrigatorio: false)]);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, InscricaoEnviadaDeTeste.Enviar(Rascunho(cpf, versao)), versao, envelope);

        Func<Task> leitura = () => Obter(portas, UsuarioComCpf(cpf));

        await leitura.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não tem o formulário de inscrição*");
    }

    [Fact(DisplayName = "Versão fixada com envelope de versão desconhecida falha a leitura, sem lista parcial")]
    public async Task Handle_VersaoDoEnvelopeDesconhecida_FalhaSemLista()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, _, _) = MontarVersaoComFormularioDeInscricao();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        codecs.LeAVersao(SchemaVersion).Returns(false);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, codecs);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "O formulário da inscrição diz a fase em que é respondido, que separa os documentos dela na avaliação")]
    public async Task Handle_FormularioComFase_PublicaAFase()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, Guid faseDaInscricao) = MontarVersaoComFormularioDeInscricao();
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.Value!.Should().ContainSingle().Which.FaseId.Should().Be(faseDaInscricao);
    }

    [Fact(DisplayName = "Com dois formulários obrigatórios e um facultativo na fase de inscrição, devolve os dois obrigatórios em ordem de código")]
    public async Task Handle_VariosFormulariosDaInscricao_DevolveTodosOsObrigatoriosEmOrdemDeCodigo()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope, Guid faseDaInscricao) = MontarVersaoComFormularioDeInscricao(
            formularios: DoisObrigatoriosEUmFacultativo);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Select(static f => f.Codigo).Should().Equal("COMPLEMENTO_DA_INSCRICAO", FormularioDeTeste.CodigoDaInscricao);
        resultado.Value.Should().OnlyContain(f => f.FaseId == faseDaInscricao && f.Obrigatorio && f.ComprovacaoDocumental == null
            && f.DependenciasDaAvaliacao != null && f.DependenciasDoFator != null);
    }
}

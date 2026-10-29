namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A geração do arquivo de envio ao INEP: o escopo vem da versão divulgada, o leiaute é do tipo
/// certo e o registro da geração guarda só o total, nunca os CPFs.
/// </summary>
public sealed class GerarEnvioAoInepCommandHandlerTests
{
    private const string Edicao = "2025";

    private readonly CenarioDoEnemDeTeste _cenario = new(Edicao);
    private readonly IInscritosDaEdicaoDoEnem _inscritos = Substitute.For<IInscritosDaEdicaoDoEnem>();
    private readonly IGeracaoDeEnvioAoInepRepository _geracoes = Substitute.For<IGeracaoDeEnvioAoInepRepository>();
    private readonly Selecao.Application.Abstractions.ISelecaoUnitOfWork _unitOfWork = Substitute.For<Selecao.Application.Abstractions.ISelecaoUnitOfWork>();
    private readonly IUserContext _usuario = Substitute.For<IUserContext>();

    public GerarEnvioAoInepCommandHandlerTests()
    {
        _usuario.UserId.Returns("sub-admin");
    }

    private Task<Result<EnvioAoInepGeradoDto>> Gerar(
        Guid? etapa = null, string edicao = Edicao, string? leiaute = "INEP_ENEM_ENVIO", string? versao = "v1", DateTimeOffset? agora = null) =>
        GerarEnvioAoInepCommandHandler.Handle(
            new GerarEnvioAoInepCommand(_cenario.ProcessoId, etapa ?? _cenario.EtapaDoEnem.Id, edicao, leiaute, versao),
            _cenario.Certame, _cenario.Processos, _cenario.Codecs, _inscritos, _geracoes, _unitOfWork, _usuario,
            new ResolvedorFusoDeTeste(), new RelogioFixo(agora ?? CenarioDoEnemDeTeste.DepoisDoEncerramento), NullLogger.Instance, CancellationToken.None);

    private void Inscritos(params string[] cpfs) =>
        _inscritos.ListarCpfsAsync(_cenario.ProcessoId, Edicao, Arg.Any<CancellationToken>()).Returns(cpfs);

    [Fact(DisplayName = "gera o arquivo com os CPFs em ordem crescente e registra a geração só com o total")]
    public async Task Gerar_CaminhoFeliz()
    {
        string[] cpfs = [.. PersonasDeTeste.Candidatas.Take(3).Select(p => p.Cpf)];
        Inscritos(cpfs[2], cpfs[0], cpfs[1]);

        Result<EnvioAoInepGeradoDto> resultado = await Gerar();

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        string[] ordenados = [.. cpfs.Order(StringComparer.Ordinal)];
        Encoding.UTF8.GetString(resultado.Value!.Conteudo).Should().Be(string.Concat(ordenados.Select(c => c + "\n")));
        resultado.Value.NomeSugerido.Should().Be("envio-inep-enem-2025.txt");
        await _geracoes.Received(1).AdicionarAsync(
            Arg.Is<GeracaoDeEnvioAoInep>(g => g.TotalDeCpfs == 3 && g.EdicaoCodigo == Edicao && g.GeradoPor == "sub-admin"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.ProcessoVivoNaoFoiConsultado();
    }

    [Fact(DisplayName = "edição sem inscrição enviada é recusada e nada é gravado")]
    public async Task Gerar_EdicaoSemInscritos_Recusa()
    {
        Inscritos();

        Result<EnvioAoInepGeradoDto> resultado = await Gerar();

        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.EdicaoSemInscritos);
        await _geracoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "etapa de outro processo, etapa sem nota do ENEM, edição não admitida e leiaute errado são recusados")]
    public async Task Gerar_EscopoInvalido_Recusa()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);

        (await Gerar(etapa: Guid.CreateVersion7())).Error!.Code.Should().Be(NotasDoEnemErrorCodes.EtapaNaoEncontrada);
        (await Gerar(etapa: _cenario.EtapaSemEnem.Id)).Error!.Code.Should().Be(NotasDoEnemErrorCodes.EtapaSemNotaDoEnem);
        (await Gerar(edicao: "2024")).Error!.Code.Should().Be(NotasDoEnemErrorCodes.EdicaoNaoAdmitida);
        (await Gerar(leiaute: "INEP_ENEM_RETORNO")).Error!.Code.Should().Be(NotasDoEnemErrorCodes.LeiauteIndisponivel);
        (await Gerar(versao: "v9")).Error!.Code.Should().Be(NotasDoEnemErrorCodes.LeiauteIndisponivel);
        (await Gerar(leiaute: null, versao: null)).Error!.Code.Should().Be(NotasDoEnemErrorCodes.LeiauteIndisponivel);
        await _geracoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "o escopo sai da versão divulgada: o que só o rascunho da retificação muda não conta")]
    public async Task Gerar_EscopoPelaVersaoDivulgada_IgnoraOProcessoVivo()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);
        // A edição 2026 existiria apenas no rascunho aberto; a divulgada só admite 2025.
        Result<EnvioAoInepGeradoDto> aceita = await Gerar();
        Result<EnvioAoInepGeradoDto> recusada = await Gerar(edicao: "2026");

        aceita.IsSuccess.Should().BeTrue();
        recusada.Error!.Code.Should().Be(NotasDoEnemErrorCodes.EdicaoNaoAdmitida);
        _cenario.ProcessoVivoNaoFoiConsultado();
    }

    [Fact(DisplayName = "processo sem versão divulgada é recusado")]
    public async Task Gerar_SemVersaoDivulgada_Recusa()
    {
        Selecao.Domain.Interfaces.ICertameDivulgadoRepository semDivulgacao = Substitute.For<Selecao.Domain.Interfaces.ICertameDivulgadoRepository>();

        Result<EnvioAoInepGeradoDto> resultado = await GerarEnvioAoInepCommandHandler.Handle(
            new GerarEnvioAoInepCommand(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, "INEP_ENEM_ENVIO", "v1"),
            semDivulgacao, _cenario.Processos, _cenario.Codecs, _inscritos, _geracoes, _unitOfWork, _usuario,
            new ResolvedorFusoDeTeste(), new RelogioFixo(CenarioDoEnemDeTeste.DepoisDoEncerramento), NullLogger.Instance, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "com as inscrições abertas, no último instante inclusive, o envio é recusado com a data de encerramento e nada é gravado")]
    public async Task Gerar_InscricoesAbertas_Recusa()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);

        foreach (DateTimeOffset agora in new[] { DivulgacaoDeTeste.FimDoPeriodo.AddDays(-5), DivulgacaoDeTeste.FimDoPeriodo })
        {
            Result<EnvioAoInepGeradoDto> resultado = await Gerar(agora: agora);

            resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.InscricoesEmAndamento);
            resultado.Error.Message.Should().Contain("30/01/2026 às 21h00");
        }

        await _geracoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "um instante depois do encerramento das inscrições o envio é gerado")]
    public async Task Gerar_LogoDepoisDoEncerramento_Gera()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);

        Result<EnvioAoInepGeradoDto> resultado = await Gerar(agora: DivulgacaoDeTeste.FimDoPeriodo.AddTicks(1));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "a retificação que prorroga as inscrições adia o envio")]
    public async Task Gerar_InscricoesProrrogadas_Recusa()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);
        DateTimeOffset novoFim = DivulgacaoDeTeste.FimDoPeriodo.AddDays(7);
        _cenario.ProrrogarInscricoesAte(novoFim);

        (await Gerar(agora: DivulgacaoDeTeste.FimDoPeriodo.AddDays(1))).Error!.Code.Should().Be(NotasDoEnemErrorCodes.InscricoesEmAndamento);
        (await Gerar(agora: novoFim.AddDays(1))).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "a retificação divulgada depois da primeira conferência e antes da gravação adia o envio")]
    public async Task Gerar_ProrrogadaDuranteATrava_Recusa()
    {
        Inscritos(PersonasDeTeste.Candidatas[0].Cpf);
        _cenario.Processos.TravarAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _cenario.ProrrogarInscricoesAte(DivulgacaoDeTeste.FimDoPeriodo.AddDays(7));
                return Task.CompletedTask;
            });

        Result<EnvioAoInepGeradoDto> resultado = await Gerar();

        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.InscricoesEmAndamento);
        await _geracoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }
}

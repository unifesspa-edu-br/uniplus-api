namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A importação do retorno do INEP: o arquivo é conferido antes de qualquer trava ou gravação, o
/// índice cego é calculado fora da trava do processo, e reimportar o arquivo do lote vigente não cria
/// outro lote.
/// </summary>
public sealed class ImportarNotasDoEnemCommandHandlerTests
{
    private const string Edicao = "2025";
    private static readonly IReadOnlyList<PersonasDeTeste.PessoaFicticia> Pessoas = PersonasDeTeste.Candidatas;

    private readonly CenarioDoEnemDeTeste _cenario = new(Edicao);
    private readonly IInscritosDaEdicaoDoEnem _inscritos = Substitute.For<IInscritosDaEdicaoDoEnem>();
    private readonly IIndiceCegoDoCpf _indices = Substitute.For<IIndiceCegoDoCpf>();
    private readonly ILoteDeNotasDoEnemRepository _lotes = Substitute.For<ILoteDeNotasDoEnemRepository>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly IUserContext _usuario = Substitute.For<IUserContext>();

    public ImportarNotasDoEnemCommandHandlerTests()
    {
        _usuario.UserId.Returns("sub-admin");
        _indices.CalcularAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(chamada => (IReadOnlyDictionary<string, IndiceCegoDoCpf>)chamada.Arg<IReadOnlyCollection<string>>()
                .ToDictionary(c => c, Indice));
        _lotes.ObterNumerosDeInscricaoAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        InscritosDaEdicao(0, 1);
    }

    private static IndiceCegoDoCpf Indice(string cpf) => IndiceCegoDoCpf.De(Encoding.UTF8.GetBytes($"indice:{cpf}"));

    private void InscritosDaEdicao(params int[] pessoas) =>
        _inscritos.ListarAsync(_cenario.ProcessoId, Edicao, Arg.Any<CancellationToken>())
            .Returns([.. pessoas.Select(i => new InscritoDaEdicao(Guid.CreateVersion7(), Indice(Pessoas[i].Cpf), []))]);

    private Task<Result<ImportacaoDeNotasDto>> Importar(byte[] conteudo, string leiaute = "INEP_ENEM_RETORNO") =>
        ImportarNotasDoEnemCommandHandler.Handle(
            new ImportarNotasDoEnemCommand(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, leiaute, "v1", conteudo),
            _cenario.Certame, _cenario.Processos, _cenario.Codecs, _inscritos, _indices, _lotes, _unitOfWork, _usuario,
            TimeProvider.System, NullLogger.Instance, CancellationToken.None);

    private static byte[] Arquivo(params int[] pessoas) => Bytes([.. pessoas.Select(i => Linha(Pessoas[i], i + 1))]);

    private static LoteDeNotasDoEnem LoteComHash(string hash, Guid processo, Guid etapa) =>
        LoteDeNotasDoEnem.Registrar(
            processo, etapa, Edicao, "INEP_ENEM_RETORNO", "v1", new string('a', 64), hash, "sub", DateTimeOffset.UtcNow, 0,
            new ResultadoDoCruzamento([], [], [], []));

    [Fact(DisplayName = "arquivo fora do leiaute é recusado sem calcular índice, sem travar o processo e sem gravar")]
    public async Task Importar_EstruturaInvalida_NaoToca()
    {
        Result<ImportacaoDeNotasDto> resultado = await Importar(Bytes([LinhaDe23Campos(Pessoas[0], 1)]));

        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.ArquivoForaDoLeiaute);
        await _indices.DidNotReceiveWithAnyArgs().CalcularAsync(default!, default);
        await _cenario.Processos.DidNotReceiveWithAnyArgs().TravarAsync(default, default);
        await _lotes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "código de situação desconhecido recusa o arquivo inteiro com o erro dedicado")]
    public async Task Importar_SituacaoDesconhecida_ErroDedicado()
    {
        byte[] arquivo = Bytes([Linha(Pessoas[0], 1, situacoes: ["7", "7", "7", "7", "9"])]);

        Result<ImportacaoDeNotasDto> resultado = await Importar(arquivo);

        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.CodigoDeSituacaoDesconhecido);
        await _lotes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "escopo inválido, leiaute de envio e arquivo vazio são recusados antes de ler o arquivo")]
    public async Task Importar_EscopoELeiaute_Recusa()
    {
        (await Importar(Arquivo(0), leiaute: "INEP_ENEM_ENVIO")).Error!.Code.Should().Be(NotasDoEnemErrorCodes.LeiauteIndisponivel);
        (await Importar([])).Error!.Code.Should().Be(NotasDoEnemErrorCodes.ArquivoVazio);

        Result<ImportacaoDeNotasDto> outraEtapa = await ImportarNotasDoEnemCommandHandler.Handle(
            new ImportarNotasDoEnemCommand(_cenario.ProcessoId, _cenario.EtapaSemEnem.Id, Edicao, "INEP_ENEM_RETORNO", "v1", Arquivo(0)),
            _cenario.Certame, _cenario.Processos, _cenario.Codecs, _inscritos, _indices, _lotes, _unitOfWork, _usuario,
            TimeProvider.System, NullLogger.Instance, CancellationToken.None);
        outraEtapa.Error!.Code.Should().Be(NotasDoEnemErrorCodes.EtapaSemNotaDoEnem);

        await _lotes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "o índice cego é calculado antes de travar o processo")]
    public async Task Importar_IndiceCalculadoAntesDaTrava()
    {
        await Importar(Arquivo(0, 1));

        Received.InOrder(() =>
        {
            _indices.CalcularAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
            _cenario.Processos.TravarAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
            _lotes.ObterVigenteAsync(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "arquivo novo cria um lote com os totais do cruzamento, do usuário autenticado")]
    public async Task Importar_ArquivoNovo_CriaLote()
    {
        Result<ImportacaoDeNotasDto> resultado = await Importar(Arquivo(0, 1, 5));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Criado.Should().BeTrue();
        resultado.Value.Lote.Vigente.Should().BeTrue();
        resultado.Value.Lote.TotalDeLinhas.Should().Be(3);
        resultado.Value.Lote.TotalDeInscritosComNota.Should().Be(2);
        resultado.Value.Lote.TotalDeLinhasSemInscrito.Should().Be(1);
        resultado.Value.Lote.ImportadoPor.Should().Be("sub-admin");
        await _lotes.Received(1).AdicionarAsync(Arg.Any<LoteDeNotasDoEnem>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "o arquivo do lote vigente devolve o lote existente, sem criar outro")]
    public async Task Importar_ArquivoDoVigente_DevolveOExistente()
    {
        byte[] arquivo = Arquivo(0, 1);
        LoteDeNotasDoEnem vigente = LoteComHash(Convert.ToHexStringLower(SHA256.HashData(arquivo)), _cenario.ProcessoId, _cenario.EtapaDoEnem.Id);
        _lotes.ObterVigenteAsync(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, Arg.Any<CancellationToken>()).Returns(vigente);
        _lotes.ObterComOcorrenciasAsync(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, vigente.Id, Arg.Any<CancellationToken>()).Returns(vigente);

        Result<ImportacaoDeNotasDto> resultado = await Importar(arquivo);

        resultado.Value!.Criado.Should().BeFalse();
        resultado.Value.Lote.Id.Should().Be(vigente.Id);
        await _lotes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "arquivo diferente do vigente, ou o de um lote já substituído, cria lote novo")]
    public async Task Importar_ArquivoDiferenteDoVigente_CriaLoteNovo()
    {
        LoteDeNotasDoEnem vigente = LoteComHash(new string('b', 64), _cenario.ProcessoId, _cenario.EtapaDoEnem.Id);
        _lotes.ObterVigenteAsync(_cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, Arg.Any<CancellationToken>()).Returns(vigente);

        Result<ImportacaoDeNotasDto> resultado = await Importar(Arquivo(0, 1));

        resultado.Value!.Criado.Should().BeTrue();
        resultado.Value.Lote.Id.Should().NotBe(vigente.Id);
        await _lotes.Received(1).AdicionarAsync(Arg.Any<LoteDeNotasDoEnem>(), Arg.Any<CancellationToken>());
    }
}

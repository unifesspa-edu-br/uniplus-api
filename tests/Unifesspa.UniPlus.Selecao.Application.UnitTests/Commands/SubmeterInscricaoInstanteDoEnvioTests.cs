namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// A avaliação do envio usa um instante só, mesmo quando o dia vira durante a chamada.
/// </summary>
public sealed class SubmeterInscricaoInstanteDoEnvioTests
{
    private static readonly FatoColetado DataAteOEnvio = FatoColetado.Criar(
        "DATA_DO_EVENTO", 1, "Data do evento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
        restricoes: [new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0))], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static InscricaoRequest RequestComDataDoEvento(Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string dataDoEvento)
    {
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> respostas = new(request.Respostas!, StringComparer.Ordinal)
        {
            ["DATA_DO_EVENTO"] = JsonSerializer.SerializeToElement(dataDoEvento),
        };
        return request with { Respostas = respostas };
    }

    [Fact(DisplayName = "O relógio que vira o dia durante o envio não muda o dia de que a faixa de data se conta")]
    public async Task Handle_ViradaDoDiaDuranteOEnvio_AvaliaNoDiaDoInstanteUnico()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // 23:59:59,9 de 15/01 em Belém; cada leitura seguinte do relógio já cai em 16/01.
        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf),
            portas: new PortasDoEnvioDeTeste(Relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200))));

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_DO_EVENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data até 15/01/2026.");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O comprovante leva o mesmo instante com que o envio foi avaliado")]
    public async Task Handle_Envio_ComprovanteLevaOInstanteDaAvaliacao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            portas: new PortasDoEnvioDeTeste(Relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200))));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Instante.Should().Be(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero));
    }

    [Fact(DisplayName = "A correção da inscrição enviada conta a faixa de data do instante da correção")]
    public async Task Handle_Correcao_ContaAFaixaDoNovoEnvio()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> envio = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            portas: new PortasDoEnvioDeTeste(Relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero))));
        envio.IsSuccess.Should().BeTrue(envio.Error?.Message);

        Result<ReciboDaInscricaoDto> correcao = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            portas: new PortasDoEnvioDeTeste(Relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero))));

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
    }

    /// <summary>Um relógio cuja leitura seguinte é sempre mais tarde que a anterior, pelo passo.</summary>
    private sealed class RelogioQueAvanca(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
    {
        private int _leituras;

        public override DateTimeOffset GetUtcNow() => inicio + (passo * _leituras++);
    }
}

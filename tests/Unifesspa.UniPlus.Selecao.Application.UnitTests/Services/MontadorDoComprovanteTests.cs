namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ComprovanteDeTeste;

/// <summary>O montador lê a versão que a inscrição fixou no primeiro envio, nunca a que o processo tem de mais recente.</summary>
public sealed class MontadorDoComprovanteTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static MontadorDoComprovante NovoMontador(IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope registro) =>
        new(processos, registro, new PrazoDaCorrecaoDoComprovante(
            Substitute.For<ICertameDivulgadoRepository>(), processos, registro, Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System),
            new CancelamentoDoComprovante(DivulgacaoDeTeste.Fuso()));

    [Fact(DisplayName = "O comprovante usa o texto da versão em que a inscrição foi enviada, mesmo com uma versão posterior de rótulos diferentes")]
    public async Task MontarAsync_UsaOTextoDaVersaoFixada()
    {
        (VersaoConfiguracao primeira, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        (VersaoConfiguracao retificada, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Inscricao inscricao = Inscricao.Iniciar(ProcessoId, PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", primeira.Id).Value!;
        ComprovanteNaPosicao posicao = new(Emitir(inscricao, Respostas(("APELIDO", "Nanda"))), 1, 2);

        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([primeira, retificada]);
        processos.ObterNomeAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns("PSS Medicina 2027");
        processos.ObterNomeDaUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns("Centro de Processos Seletivos");
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns([new CapacidadeCodec(primeira.SchemaVersion, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registro.Reidratar(primeira).Returns(Result<EnvelopeReidratado>.Success(Envelope([Texto("APELIDO", "Apelido")])));
        registro.Reidratar(retificada).Returns(Result<EnvelopeReidratado>.Success(Envelope([Texto("APELIDO", "Como prefere ser chamada")])));

        Result<ComprovanteDaInscricaoDto> resultado = await NovoMontador(processos, registro).MontarAsync(inscricao, posicao, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        TodosOsItens(resultado.Value!).Should().ContainSingle().Which.Rotulo.Should().Be("Apelido");
        resultado.Value!.UnidadeAdministradora.Should().Be("Centro de Processos Seletivos");
        registro.DidNotReceive().Reidratar(retificada);
    }

    [Fact(DisplayName = "A inscrição presa a uma versão que o processo não tem recusa com a versão publicada ausente")]
    public async Task MontarAsync_VersaoFixadaInexistente_Recusa()
    {
        (VersaoConfiguracao outra, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Inscricao inscricao = UmaInscricao();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterVersoesAsync(inscricao.ProcessoSeletivoId, Arg.Any<CancellationToken>()).Returns([outra]);

        Result<ComprovanteDaInscricaoDto> resultado = await NovoMontador(processos, Substitute.For<IRegistroCodecsEnvelope>())
            .MontarAsync(inscricao, new ComprovanteNaPosicao(Emitir(inscricao, Respostas()), 1, 1), CancellationToken.None);

        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }
}

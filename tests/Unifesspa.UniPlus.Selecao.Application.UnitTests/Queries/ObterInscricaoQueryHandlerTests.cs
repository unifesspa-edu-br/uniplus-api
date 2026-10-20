namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Ler a inscrição (issue #1859, <c>GET .../inscricao</c>): devolve a do dono do token, pelo CPF do
/// claim, e confere o <c>sub</c> gravado — nunca por id na rota.
/// </summary>
public sealed class ObterInscricaoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(ProcessoId), inscricaoRepository, Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), userContext, DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada, sem citar o CPF nem o processo")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        Result<InscricaoDto> resultado = await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(ProcessoId), inscricaoRepository, Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
        resultado.Error.Message.Should().NotContain(cpf.Valor).And.NotContain(ProcessoId.ToString());
    }

    [Fact(DisplayName = "Sub diferente do gravado para o mesmo CPF é recusado como dono divergente")]
    public async Task Handle_SubDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "sub-original", "https://issuer", Guid.CreateVersion7()).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        Result<InscricaoDto> resultado = await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(ProcessoId), inscricaoRepository, Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), UsuarioComCpf(cpf, sub: "sub-outro"), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
    }

    [Fact(DisplayName = "Inscrição do dono é devolvida com o ETag corrente e a avaliação da versão que ela fixou")]
    public async Task Handle_InscricaoDoDono_Devolve()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Inscricao existente = Inscricao.Iniciar(ProcessoId, cpf, "sub-1", "https://issuer", versao.Id).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<InscricaoDto> resultado = await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(ProcessoId), inscricaoRepository, DivulgacaoDeTeste.Certame(ProcessoId), processoRepository, registroCodecs, DivulgacaoDeTeste.Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Id.Should().Be(existente.Id);
        resultado.Value!.ETag.Should().Be(existente.ETag);
        ModalidadesDaInscricaoDto modalidades = resultado.Value!.Avaliacao.Modalidades;
        modalidades.Derivada.Should().BeTrue("a modalidade sai das modalidades que a versão oferta");
        modalidades.Situacao.Should().Be("CONCORRE");
        modalidades.Itens.Select(static m => m.Codigo).Should().Equal(["AC"], "a única ofertada é a ampla, incondicional");
    }
}

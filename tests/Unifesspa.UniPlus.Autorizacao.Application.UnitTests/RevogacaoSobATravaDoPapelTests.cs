namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// A revogação da concessão de Representante do controlador toma a trava do papel antes da trava da concessão, para não
/// cruzar com a designação concorrente; a revogação de outro perfil não toca o papel (ADR-0162).
/// </summary>
public sealed class RevogacaoSobATravaDoPapelTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string EmailDoBeneficiario = "leandro-viana84@uniplus-test.local";
    private const string Motivo = "Deixou a função.";

    private static readonly Identidade Leandro = new(Emissor, "leandro.viana");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");

    private readonly IConcessaoRepository _repositorio = Substitute.For<IConcessaoRepository>();
    private readonly IPapelDoRepresentante _papel = Substitute.For<IPapelDoRepresentante>();
    private readonly IAutorizacaoUnitOfWork _unidade = Substitute.For<IAutorizacaoUnitOfWork>();

    [Fact(DisplayName = "A revogação do Representante trava o papel antes de travar a concessão")]
    public async Task Revogar_Representante_TravaOPapelAntesDaConcessao()
    {
        Concessao concessao = Preparar(PerfisDeSistema.RepresentanteDoControladorId);

        Result resultado = await RevogarAsync(concessao);

        resultado.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = _papel.TravarAsync(Arg.Any<CancellationToken>());
            _ = _repositorio.ObterParaAlterarAsync(concessao.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A revogação de concessão de perfil comum não toma a trava do papel")]
    public async Task Revogar_PerfilComum_NaoTravaOPapel()
    {
        Concessao concessao = Preparar(Guid.CreateVersion7());

        (await RevogarAsync(concessao)).IsSuccess.Should().BeTrue();

        await _papel.DidNotReceive().TravarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A concessão inexistente responde não encontrada sem tomar a trava do papel")]
    public async Task Revogar_ConcessaoInexistente_NaoTravaOPapel()
    {
        Result resultado = await RevogarAsync(Preparar(PerfisDeSistema.RepresentanteDoControladorId), Guid.CreateVersion7());

        resultado.IsFailure.Should().BeTrue();
        await _papel.DidNotReceive().TravarAsync(Arg.Any<CancellationToken>());
    }

    private Concessao Preparar(Guid perfilId)
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Perfil de teste", "Perfil de teste.", ["autorizacao:revisoes:conduzir"], new CatalogoLivre());
        Concessao concessao = Concessao.Conceder(
            Leandro, perfil, EscopoDaConcessao.DaInstituicao(), new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            Diogo, DateTimeOffset.UtcNow, new CatalogoLivre(), beneficiarioEmail: EmailDoBeneficiario).Value!;
        typeof(Concessao).GetProperty(nameof(Concessao.PerfilId))!.SetValue(concessao, perfilId);
        _repositorio.ObterComPerfilAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(new ConcessaoComPerfil(concessao, perfil.Nome, null));
        _repositorio.ObterParaAlterarAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(concessao);
        return concessao;
    }

    private Task<Result> RevogarAsync(Concessao concessao, Guid? outroId = null) =>
        RevogarConcessaoCommandHandler.Handle(
            new RevogarConcessaoCommand(outroId ?? concessao.Id, Motivo, EmailDoBeneficiario),
            _repositorio, _papel, _unidade, Autor(), TimeProvider.System, CancellationToken.None);

    private static IUserContext Autor()
    {
        IUserContext autor = Substitute.For<IUserContext>();
        autor.Issuer.Returns(Emissor);
        autor.UserId.Returns(Diogo.Sub);
        return autor;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// A manifestação técnica num item com aprovação solicitada (UNI-REQ-0373): grava o evento e muda o circuito do item na
/// mesma transação, sem decidir.
/// </summary>
public sealed class ManifestacaoNaProtecaoDoCatalogoTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IManifestacaoDaProtecaoRepository _manifestacoes = Substitute.For<IManifestacaoDaProtecaoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "A manifestação no tipo solicitado grava o evento de quem se manifestou e marca o circuito, sem decidir")]
    public async Task Tipo_RegistraEGrava()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado());

        Result resultado = await ManifestarAsync("TIPO_DOCUMENTO", laudo.Id, 1, "INADEQUADA", "O laudo revela condição de saúde.");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EventoGravado().Should().Match<ManifestacaoDaProtecaoDoCatalogo>(m =>
            m.Parecer == ParecerDaManifestacao.Inadequada && m.Autor.Sub == Isabella && m.Instante == Agora && m.SubstituiId == null);
        (laudo.Aprovacao.Situacao, laudo.Aprovacao.ManifestacaoDesfavoravelVigente, laudo.Aprovacao.ManifestadaEm)
            .Should().Be((SituacaoDaAprovacao.Solicitada, true, Agora));
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A nova manifestação da mesma pessoa no fato substitui a vigente dela, lida do ciclo do item")]
    public async Task Fato_SubstituiAVigenteDaMesmaPessoa()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA", autor: UsuarioComoAutor());
        FatoCandidato fato = Fato(montagem);
        _manifestacoes.ListarDoCicloAsync(new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id), CicloDaAprovacao.Primeiro, Arg.Any<CancellationToken>())
            .Returns(montagem.Eventos);

        Result resultado = await ManifestarAsync("FATO", fato.Id, 1, "ADEQUADA", null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EventoGravado().SubstituiId.Should().Be(montagem.Eventos[0].Id);
        fato.Aprovacao.ManifestacaoDesfavoravelVigente.Should().BeFalse();
    }

    [Fact(DisplayName = "O ciclo que mudou depois de lido e o item fora de solicitação são recusados sem gravar")]
    public async Task Recusas_NaoGravam()
    {
        TipoDocumento solicitado = Tipo(new ItemSobAprovacaoBuilder().Solicitado());
        TipoDocumento pendente = Tipo(new ItemSobAprovacaoBuilder());

        Result cicloMudou = await ManifestarAsync("TIPO_DOCUMENTO", solicitado.Id, 2, "ADEQUADA", null);
        Result foraDeSolicitacao = await ManifestarAsync("TIPO_DOCUMENTO", pendente.Id, 1, "ADEQUADA", null);

        cicloMudou.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CicloMudou);
        foraDeSolicitacao.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        _manifestacoes.DidNotReceiveWithAnyArgs().Adicionar(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "A corrida perdida responde conflito de concorrência e descarta o que não foi salvo")]
    public async Task Concorrencia_RespondeConflito()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado());
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new DbUpdateConcurrencyException()));

        Result resultado = await ManifestarAsync("TIPO_DOCUMENTO", laudo.Id, 1, "ADEQUADA", null);

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A manifestação sobre item inexistente responde o não encontrado; a forma inválida não lê nada")]
    public async Task Inexistente_EFormaInvalida()
    {
        (await ManifestarAsync("FATO", Guid.NewGuid(), 1, "ADEQUADA", null)).Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaoEncontrado);
        (await ManifestarAsync("TIPO_DOCUMENTO", Guid.NewGuid(), 0, "ADEQUADA", null)).Errors.Should().ContainSingle(e => e.Field == "ciclo");
        await _tipos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    private static Domain.ValueObjects.AtorDoAto UsuarioComoAutor() => new(Emissor, Isabella, "Isabella Priscila Figueiredo");

    private TipoDocumento Tipo(ItemSobAprovacaoBuilder montagem)
    {
        TipoDocumento tipo = montagem.Tipo();
        _tipos.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        return tipo;
    }

    private FatoCandidato Fato(ItemSobAprovacaoBuilder montagem)
    {
        FatoCandidato fato = montagem.Fato();
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        return fato;
    }

    private ManifestacaoDaProtecaoDoCatalogo EventoGravado() =>
        (ManifestacaoDaProtecaoDoCatalogo)_manifestacoes.ReceivedCalls()
            .Should().ContainSingle(c => c.GetMethodInfo().Name == nameof(IManifestacaoDaProtecaoRepository.Adicionar)).Which.GetArguments()[0]!;

    private Task<Result> ManifestarAsync(string cadastro, Guid id, int? ciclo, string parecer, string? texto) =>
        RegistrarManifestacaoDaProtecaoCommandHandler.Handle(
            new RegistrarManifestacaoDaProtecaoCommand(new ItemInformado(cadastro, id), ciclo, new ConteudoInformado(parecer, texto)),
            new ManifestacaoNaProtecaoDoCatalogo(new ItensSobAprovacao(_tipos, _fatos), _manifestacoes, _unitOfWork, Relogio()),
            UsuarioIsabella(),
            CancellationToken.None);
}

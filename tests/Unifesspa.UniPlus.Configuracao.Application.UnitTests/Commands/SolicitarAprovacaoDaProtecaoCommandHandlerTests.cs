namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// A solicitação reúne de uma vez os itens pendentes dos cadastros que quem envia mantém e só então avisa quem
/// aprova (ADR-0153).
/// </summary>
public sealed class SolicitarAprovacaoDaProtecaoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "A solicitação leva os tipos e fatos pendentes e avisa cada titular da permissão de aprovar")]
    public async Task Handle_ReuneOsPendentesEAvisa()
    {
        TipoDocumento laudo = NovoTipo();
        TipoDocumento comprovante = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        FatoCandidato fato = NovoFato();
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([laudo, comprovante]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([fato]);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter, PermissaoConfiguracao.FatosCandidatoManter),
            new[] { Caroline, "leandro.viana@unifesspa.edu.br" });

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(2, 1));
        new[] { laudo.Aprovacao, comprovante.Aprovacao, fato.Aprovacao }.Should().OnlyContain(a =>
            a.Situacao == SituacaoDaAprovacao.Solicitada && a.SolicitadaPor == Isabella && a.SolicitadaEm == Agora);
        mensagens.Should().AllBeOfType<EnviarEmail>().Which.Select(m => m.Destinatario)
            .Should().BeEquivalentTo(Caroline, "leandro.viana@unifesspa.edu.br");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O e-mail da solicitação não leva nome de pessoa nem nome de item")]
    public async Task Handle_EmailSemDadoPessoal()
    {
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([NovoTipo()]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);

        (_, IEnumerable<object> mensagens) = await EnviarAsync(AcessoCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline });

        EnviarEmail email = mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>().Subject;
        email.CorpoTexto.Should().Contain("1 tipo de documento").And.NotContain(Isabella).And.NotContain("Laudo");
        email.Finalidade.Should().Be("catalogo.protecao.solicitacao");
    }

    [Fact(DisplayName = "Quem só mantém os tipos de documento envia só os tipos")]
    public async Task Handle_SoOsCadastrosQueQuemEnviaMantem()
    {
        FatoCandidato fato = NovoFato();
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([NovoTipo()]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([fato]);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline });

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(1, 0));
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _fatos.DidNotReceive().ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem permissão de manter nenhum dos cadastros a solicitação é recusada")]
    public async Task Handle_SemPermissao_Recusa()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar), new[] { Caroline });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem item pendente a solicitação é recusada e ninguém é avisado")]
    public async Task Handle_SemPendente_Recusa()
    {
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter, PermissaoConfiguracao.FatosCandidatoManter), new[] { Caroline });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemItemPendente);
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem ninguém com a permissão de aprovar a solicitação é recusada e os itens continuam pendentes")]
    public async Task Handle_SemQuemAprove_RecusaSemMudarOsItens()
    {
        TipoDocumento laudo = NovoTipo();
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([laudo]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter), Array.Empty<string>());

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemQuemAprove);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Só o destinatário válido conta: com a lista só de endereços inválidos a solicitação é recusada e os itens continuam pendentes")]
    public async Task Handle_SoDestinatarioInvalido_RecusaSemMudarOsItens()
    {
        TipoDocumento laudo = NovoTipo();
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([laudo]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { "sem-arroba", " " });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemQuemAprove);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A transição que o item recusa falha alto: nada é gravado e as mudanças do lote são descartadas")]
    public async Task Handle_TransicaoRecusada_FalhaSemGravar()
    {
        TipoDocumento pendente = NovoTipo();
        TipoDocumento jaSolicitado = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        jaSolicitado.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _tipos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([pendente, jaSolicitado]);
        _fatos.ListarPendentesDeSolicitacaoAsync(Arg.Any<CancellationToken>()).Returns([]);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            AcessoCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        mensagens.Should().BeEmpty();
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> EnviarAsync(
        AcessoAoCatalogoDeProtecao acesso,
        IReadOnlyList<string> destinatarios) =>
        SolicitarAprovacaoDaProtecaoCommandHandler.Handle(
            new SolicitarAprovacaoDaProtecaoCommand(acesso, destinatarios), _tipos, _fatos, _unitOfWork, UsuarioIsabella(), Relogio(), CancellationToken.None);
}

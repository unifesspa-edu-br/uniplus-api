namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
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
/// A solicitação reúne de uma vez os itens pendentes que quem envia escolheu, dos cadastros que mantém, e só então
/// avisa quem aprova (ADR-0153). O aviso a quem se manifesta está em <see cref="SolicitacaoAvisaQuemSeManifestaTests"/>.
/// </summary>
public sealed class SolicitarAprovacaoDaProtecaoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    // O repositório devolve, dos ids pedidos, só os que estão pendentes.
    private void PendentesDeTipo(params TipoDocumento[] pendentes) =>
        _tipos.ListarPendentesPorIdAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(chamada => (IReadOnlyList<TipoDocumento>)[.. pendentes.Where(t => chamada.Arg<IReadOnlyCollection<Guid>>().Contains(t.Id))]);

    private void PendentesDeFato(params FatoCandidato[] pendentes) =>
        _fatos.ListarPendentesPorIdAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(chamada => (IReadOnlyList<FatoCandidato>)[.. pendentes.Where(f => chamada.Arg<IReadOnlyCollection<Guid>>().Contains(f.Id))]);

    private static ItemParaSolicitarDto Tipo(TipoDocumento tipo) => new("TIPO_DOCUMENTO", tipo.Id);

    private static ItemParaSolicitarDto Fato(FatoCandidato fato) => new("FATO", fato.Id);

    [Fact(DisplayName = "A solicitação leva os tipos e fatos pendentes e avisa cada titular da permissão de aprovar")]
    public async Task Handle_ReuneOsPendentesEAvisa()
    {
        TipoDocumento laudo = NovoTipo();
        TipoDocumento comprovante = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        FatoCandidato fato = NovoFato();
        PendentesDeTipo(laudo, comprovante);
        PendentesDeFato(fato);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter, PermissaoConfiguracao.FatosCandidatoManter),
            new[] { Caroline, "leandro.viana@unifesspa.edu.br" }, Tipo(laudo), Tipo(comprovante), Fato(fato));

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(2, 1));
        DevemEstarSolicitadas(laudo.Aprovacao, comprovante.Aprovacao, fato.Aprovacao);
        mensagens.Should().AllBeOfType<EnviarEmail>().Which.Select(m => m.Destinatario).Should().BeEquivalentTo(Caroline, "leandro.viana@unifesspa.edu.br");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static void DevemEstarSolicitadas(params AprovacaoDaProtecao[] aprovacoes) =>
        aprovacoes.Should().OnlyContain(a => a.Situacao == SituacaoDaAprovacao.Solicitada && a.SolicitadaPor == Isabella && a.SolicitadaEm == Agora);

    [Fact(DisplayName = "O e-mail da solicitação não leva nome de pessoa nem nome de item")]
    public async Task Handle_EmailSemDadoPessoal()
    {
        TipoDocumento laudo = NovoTipo();
        PendentesDeTipo(laudo);
        PendentesDeFato();

        (_, IEnumerable<object> mensagens) = await EnviarAsync(VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, Tipo(laudo));

        EnviarEmail email = mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>().Subject;
        email.CorpoTexto.Should().Contain("1 tipo de documento").And.NotContain(Isabella).And.NotContain("Laudo");
        email.Finalidade.Should().Be("catalogo.protecao.solicitacao");
    }

    [Fact(DisplayName = "Só os itens escolhidos são enviados; os demais pendentes continuam pendentes")]
    public async Task Handle_EnviaSoOsEscolhidos()
    {
        TipoDocumento laudo = NovoTipo();
        TipoDocumento comprovante = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        FatoCandidato fato = NovoFato();
        PendentesDeTipo(laudo, comprovante);
        PendentesDeFato(fato);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter, PermissaoConfiguracao.FatosCandidatoManter), new[] { Caroline }, Tipo(laudo));

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(1, 0));
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        comprovante.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _fatos.DidNotReceiveWithAnyArgs().ListarPendentesPorIdAsync(default!, default);
    }

    [Fact(DisplayName = "O item repetido na lista conta uma vez")]
    public async Task Handle_ItemRepetido_ContaUmaVez()
    {
        TipoDocumento laudo = NovoTipo();
        PendentesDeTipo(laudo);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, Tipo(laudo), Tipo(laudo));

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(1, 0));
        mensagens.Should().ContainSingle();
    }

    [Fact(DisplayName = "Item de um cadastro que quem envia não mantém é recusado como falta de permissão, sem ler nem gravar")]
    public async Task Handle_ItemDeCadastroSemPermissao_Recusa()
    {
        TipoDocumento laudo = NovoTipo();
        FatoCandidato fato = NovoFato();

        IVerificadorDeAcesso verificador = VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter);
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(verificador, new[] { Caroline }, Tipo(laudo), Fato(fato));

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo);
        await _tipos.DidNotReceiveWithAnyArgs().ListarPendentesPorIdAsync(default!, default);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        NegativasRegistradas(verificador).Should().Equal(PermissaoConfiguracao.FatosCandidatoManter.Codigo);
    }

    [Fact(DisplayName = "A lista vazia é recusada com código próprio")]
    public async Task Handle_ListaVazia_Recusa()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SolicitacaoSemItem);
        mensagens.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item nulo da lista é recusado no campo do item, sem ler nada")]
    public async Task Handle_ItemNulo_RecusaNoCampo()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, new ItemParaSolicitarDto?[] { null });

        resposta.Errors.Should().ContainSingle(e => e.Field == "itens[0]")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
        await _tipos.DidNotReceiveWithAnyArgs().ListarPendentesPorIdAsync(default!, default);
    }

    [Fact(DisplayName = "O cadastro é comparado sem os espaços das pontas e só os ids pedidos são lidos")]
    public async Task Handle_CadastroComEspacos_LeSoOsIdsPedidos()
    {
        TipoDocumento laudo = NovoTipo();
        PendentesDeTipo(laudo);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, new ItemParaSolicitarDto(" TIPO_DOCUMENTO ", laudo.Id));

        resposta.Value.Should().Be(new SolicitacaoDaProtecaoDto(1, 0));
        await _tipos.Received(1).ListarPendentesPorIdAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == laudo.Id), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item sem id é recusado no campo do item, sem ler nada")]
    public async Task Handle_ItemSemId_RecusaNoCampo()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline },
            new ItemParaSolicitarDto("TIPO_DOCUMENTO", Guid.NewGuid()), new ItemParaSolicitarDto("TIPO_DOCUMENTO", Guid.Empty));

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "itens[1].id" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.ItemNaoPendente);
        await _tipos.DidNotReceiveWithAnyArgs().ListarPendentesPorIdAsync(default!, default);
    }

    [Fact(DisplayName = "O cadastro fora do vocabulário é recusado no campo do item")]
    public async Task Handle_CadastroInvalido_RecusaNoCampo()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, new ItemParaSolicitarDto("MODALIDADE", Guid.NewGuid()));

        resposta.Errors.Should().ContainSingle(e => e.Field == "itens[0].cadastro")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
    }

    [Fact(DisplayName = "Sem permissão de manter nenhum dos cadastros a solicitação é recusada")]
    public async Task Handle_SemPermissao_Recusa()
    {
        IVerificadorDeAcesso verificador = VerificadorCom(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar);
        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(verificador, new[] { Caroline });

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        NegativasRegistradas(verificador).Should().BeEquivalentTo(
            [PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo]);
    }

    [Fact(DisplayName = "Sem nenhuma das quatro permissões do catálogo, as quatro negativas são registradas uma vez só")]
    public async Task Handle_SemNenhumaPermissao_RegistraAsQuatroNegativasUmaVez()
    {
        IVerificadorDeAcesso verificador = VerificadorCom();

        await EnviarAsync(verificador, new[] { Caroline });

        NegativasRegistradas(verificador).Should().HaveCount(4);
    }

    [Fact(DisplayName = "Quem só consulta a proteção do catálogo não solicita: a recusa grava as negativas dos dois cadastros")]
    public async Task Handle_SoConsulta_Recusa()
    {
        IVerificadorDeAcesso verificador = VerificadorCom(PermissaoConfiguracao.ProtecaoDoCatalogoConsultar);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(verificador, new[] { Caroline }, Tipo(NovoTipo()));

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo);
        NegativasRegistradas(verificador).Should().BeEquivalentTo(
            PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static IEnumerable<string> NegativasRegistradas(IVerificadorDeAcesso verificador) =>
        verificador.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.RegistrarDesfechoAsync))
            .SelectMany(static c => (IReadOnlyCollection<DecisaoDeAcesso>)c.GetArguments()[0]!)
            .OfType<DecisaoNegada>()
            .Select(static d => d.Registro.Permissao);

    [Fact(DisplayName = "O item que não existe, ou que já saiu de pendente, é recusado e nenhum outro da lista é enviado")]
    public async Task Handle_ItemNaoPendente_RecusaTudo()
    {
        TipoDocumento pendente = NovoTipo();
        TipoDocumento jaSolicitado = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        jaSolicitado.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        PendentesDeTipo(pendente);

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, Tipo(pendente), Tipo(jaSolicitado), new ItemParaSolicitarDto("TIPO_DOCUMENTO", Guid.NewGuid()));

        resposta.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("itens[1].id", ProtecaoDoCatalogoErrorCodes.ItemNaoPendente), ("itens[2].id", ProtecaoDoCatalogoErrorCodes.ItemNaoPendente)]);
        mensagens.Should().BeEmpty();
        pendente.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem ninguém com a permissão de aprovar a solicitação é recusada e os itens continuam pendentes")]
    public async Task Handle_SemQuemAprove_RecusaSemMudarOsItens()
    {
        TipoDocumento laudo = NovoTipo();
        PendentesDeTipo(laudo);
        PendentesDeFato();

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), Array.Empty<string>(), Tipo(laudo));

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemQuemAprove);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Só o destinatário válido conta: com a lista só de endereços inválidos a solicitação é recusada e os itens continuam pendentes")]
    public async Task Handle_SoDestinatarioInvalido_RecusaSemMudarOsItens()
    {
        TipoDocumento laudo = NovoTipo();
        PendentesDeTipo(laudo);
        PendentesDeFato();

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { "sem-arroba", " " }, Tipo(laudo));

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
        PendentesDeTipo(pendente, jaSolicitado);
        PendentesDeFato();

        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, Tipo(pendente), Tipo(jaSolicitado));

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        mensagens.Should().BeEmpty();
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A solicitação registra um ato por item enviado, com quem enviou e a proteção do item")]
    public async Task Handle_RegistraUmAtoPorItem()
    {
        TipoDocumento laudo = NovoTipo();
        TipoDocumento comprovante = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        FatoCandidato fato = NovoFato();
        PendentesDeTipo(laudo, comprovante);
        PendentesDeFato(fato);

        await EnviarAsync(
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter, PermissaoConfiguracao.FatosCandidatoManter), new[] { Caroline }, Tipo(laudo), Tipo(comprovante), Fato(fato));

        AtoDaProtecaoDoCatalogo[] registrados = AtosRegistrados();
        registrados.Should().HaveCount(3).And.OnlyContain(static a => EhASolicitacaoDaIsabella(a));
        registrados.Select(a => (a.Cadastro, a.ItemId, a.ItemCodigo)).Should().BeEquivalentTo(
            [(CadastroDoCatalogo.TipoDocumento, laudo.Id, "LAUDO_MEDICO_COMPLEMENTAR"), (CadastroDoCatalogo.TipoDocumento, comprovante.Id, "COMPROVANTE_RESIDENCIA"), (CadastroDoCatalogo.Fato, fato.Id, "TEM_RENDA")]);
    }

    private AtoDaProtecaoDoCatalogo[] AtosRegistrados() =>
        [.. _atos.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IAtoDaProtecaoDoCatalogoRepository.Adicionar))
            .Select(static c => (AtoDaProtecaoDoCatalogo)c.GetArguments()[0]!)];

    private static bool EhASolicitacaoDaIsabella(AtoDaProtecaoDoCatalogo ato) =>
        (ato.Ato, ato.AtorSub, ato.AtorEmissor, ato.AtorNome, ato.Instante) == (AtoDaProtecao.Solicitacao, Isabella, Emissor, "Isabella Figueiredo", Agora)
        && ato.Justificativa is null && ato.ClassificacaoRecomendada is null;

    [Fact(DisplayName = "A solicitação recusada não registra ato")]
    public async Task Handle_Recusada_NaoRegistraAto()
    {
        TipoDocumento pendente = NovoTipo();
        TipoDocumento jaSolicitado = NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência");
        jaSolicitado.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        PendentesDeTipo(pendente, jaSolicitado);

        await EnviarAsync(VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter), new[] { Caroline }, Tipo(pendente), Tipo(jaSolicitado));

        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
    }

    [Fact(DisplayName = "Sem sujeito no token, o próprio handler recusa com identidade incompleta, sem ler nem gravar")]
    public async Task Handle_SemSujeito_RecusaComIdentidadeIncompleta()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(SemSujeito.Instancia);

        (Result<SolicitacaoDaProtecaoDto> resposta, _) = await EnviarAsync(verificador, new[] { Caroline }, new ItemParaSolicitarDto("TIPO_DOCUMENTO", Guid.NewGuid()));

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    private Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> EnviarAsync(
        IVerificadorDeAcesso verificador,
        IReadOnlyList<string> destinatarios,
        params ItemParaSolicitarDto?[] itens)
    {
        ITitularesDaPermissao titulares = Substitute.For<ITitularesDaPermissao>();
        titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, Arg.Any<CancellationToken>()).Returns(destinatarios);
        titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoManifestar.Codigo, Arg.Any<CancellationToken>()).Returns([]);

        return SolicitarAprovacaoDaProtecaoCommandHandler.Handle(
            new SolicitarAprovacaoDaProtecaoCommand(itens),
            new SolicitacaoDaProtecaoDoCatalogo(_tipos, _fatos, _atos, _unitOfWork, titulares, Relogio()),
            verificador,
            UsuarioIsabella(),
            CancellationToken.None);
    }
}

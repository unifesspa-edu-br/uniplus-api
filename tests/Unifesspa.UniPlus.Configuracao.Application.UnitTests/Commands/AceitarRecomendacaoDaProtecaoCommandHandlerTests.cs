namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>O aceite da recomendação da proteção de dados aprova o item e avisa quem aprova (ADR-0153).</summary>
public sealed class AceitarRecomendacaoDaProtecaoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Aceitar a recomendação aprova o tipo como sensível, grava e avisa o Representante do controlador")]
    public async Task Tipo_AceitaARecomendacao()
    {
        TipoDocumento laudo = TipoComAjusteRecomendado(ClassificacaoDeProtecao.Sensivel);
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        (Result resposta, IEnumerable<object> mensagens) = await AceitarTipoAsync(laudo.Id);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        laudo.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
        EnviarEmail email = mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>().Subject;
        email.Destinatario.Should().Be(Caroline);
        email.Finalidade.Should().Be("catalogo.protecao.aceite");
        email.CorpoTexto.Should().Contain("Laudo médico complementar").And.Contain("sensível").And.NotContain(Isabella);
        email.Assunto.Should().Be("Recomendação do Representante do controlador aceita no catálogo de tipos de documento e de fatos do candidato");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O aceite sobre o tipo alterado concorrentemente devolve conflito, descarta e não avisa ninguém")]
    public async Task Tipo_AlteradoConcorrentemente_DevolveConflito()
    {
        TipoDocumento laudo = TipoComAjusteRecomendado(ClassificacaoDeProtecao.Sensivel);
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new DbUpdateConcurrencyException()));

        (Result resposta, IEnumerable<object> mensagens) = await AceitarTipoAsync(laudo.Id);

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia);
        mensagens.Should().BeEmpty();
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "O nome do item vai escapado no HTML do aviso")]
    public async Task Tipo_NomeEscapadoNoHtml()
    {
        TipoDocumento tipo = TipoComAjusteRecomendado(ClassificacaoDeProtecao.Pessoal, nome: "Laudo <b>& cia</b>");
        _tipos.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        (_, IEnumerable<object> mensagens) = await AceitarTipoAsync(tipo.Id);

        mensagens.Cast<EnviarEmail>().Single().CorpoHtml.Should().Contain(WebUtility.HtmlEncode("Laudo <b>& cia</b>")).And.NotContain("<b>&");
    }

    [Fact(DisplayName = "Aceitar o que não teve ajuste recomendado é recusado, sem gravar nem avisar")]
    public async Task Tipo_SemAjusteRecomendado_Recusa()
    {
        TipoDocumento laudo = NovoTipo();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        (Result resposta, IEnumerable<object> mensagens) = await AceitarTipoAsync(laudo.Id);

        resposta.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar);
        mensagens.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O aceite vale para o fato de sistema e não muda o resto da definição")]
    public async Task Fato_DeSistema_AceitaARecomendacao()
    {
        FatoCandidato corRaca = CorRacaComAjusteRecomendado();
        _fatos.ObterPorIdAsync(corRaca.Id, Arg.Any<CancellationToken>()).Returns(corRaca);
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([corRaca]);

        (Result resposta, IEnumerable<object> mensagens) = await AceitarFatoAsync(corRaca.Id);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        corRaca.Should().BeEquivalentTo(new { ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel, Sistema = true, Codigo = "COR_RACA" });
        corRaca.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>().Which.CorpoTexto.Should().Contain("fato");
        _atos.Received(1).Adicionar(Arg.Is<AtoDaProtecaoDoCatalogo>(ato =>
            ato.Ato == AtoDaProtecao.Aceite && ato.Cadastro == CadastroDoCatalogo.Fato && ato.ItemCodigo == "COR_RACA"
            && ato.ClassificacaoProtecao == ClassificacaoDeProtecao.Sensivel));
    }

    private static FatoCandidato CorRacaComAjusteRecomendado()
    {
        FatoCandidato corRaca = FatoCandidato.Criar(
            "COR_RACA", "Cor ou raça", "Descrição do fato.", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            null, "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade,
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal, sistema: true).Value!;
        corRaca.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        corRaca.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Agora);

        return corRaca;
    }

    // O fato aceito não tem precedência, modelo nem modalidade que o use.
    private Task<(Result Resposta, IEnumerable<object> Mensagens)> AceitarFatoAsync(Guid fatoId)
    {
        IPrecedenciaFaseRepository precedencias = Substitute.For<IPrecedenciaFaseRepository>();
        precedencias.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);
        IModeloFormularioRepository modelos = Substitute.For<IModeloFormularioRepository>();
        modelos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([]);
        IModalidadeRepository modalidades = Substitute.For<IModalidadeRepository>();
        modalidades.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);

        return AceitarRecomendacaoDoFatoCandidatoCommandHandler.Handle(
            new AceitarRecomendacaoDoFatoCandidatoCommand(fatoId, [Caroline]), _fatos, _atos, precedencias, modelos, modalidades,
            _unitOfWork, UsuarioIsabella(), Relogio(), CancellationToken.None);
    }

    [Fact(DisplayName = "O aceite do tipo registra o ato com a classificação aplicada e quem aceitou")]
    public async Task Tipo_RegistraOAceite()
    {
        TipoDocumento laudo = TipoComAjusteRecomendado(ClassificacaoDeProtecao.Sensivel);
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        await AceitarTipoAsync(laudo.Id);

        _atos.Received(1).Adicionar(Arg.Is<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.Aceite
            && a.Cadastro == CadastroDoCatalogo.TipoDocumento
            && a.ItemId == laudo.Id
            && a.ClassificacaoProtecao == ClassificacaoDeProtecao.Sensivel
            && a.ClassificacaoRecomendada == null
            && a.AtorSub == Isabella
            && a.AtorNome == "Isabella Figueiredo"));
    }

    [Fact(DisplayName = "O aceite recusado não registra ato")]
    public async Task Tipo_Recusado_NaoRegistraAto()
    {
        TipoDocumento laudo = NovoTipo();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        await AceitarTipoAsync(laudo.Id);

        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
    }

    private static TipoDocumento TipoComAjusteRecomendado(ClassificacaoDeProtecao recomendada, string nome = "Laudo médico complementar")
    {
        TipoDocumento tipo = NovoTipo(nome: nome);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        tipo.RecomendarAjusteDaProtecao(recomendada, null, Caroline, Agora);
        return tipo;
    }

    private Task<(Result Resposta, IEnumerable<object> Mensagens)> AceitarTipoAsync(Guid id) =>
        AceitarRecomendacaoDoTipoDocumentoCommandHandler.Handle(
            new AceitarRecomendacaoDoTipoDocumentoCommand(id, [Caroline]), _tipos, _atos, _unitOfWork,
            UsuarioIsabella(), Relogio(), CancellationToken.None);
}

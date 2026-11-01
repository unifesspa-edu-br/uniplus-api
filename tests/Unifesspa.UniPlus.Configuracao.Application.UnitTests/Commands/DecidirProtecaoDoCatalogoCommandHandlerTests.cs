namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>A decisão da proteção de dados sobre o tipo de documento e o fato que estão numa solicitação (ADR-0153).</summary>
public sealed class DecidirProtecaoDoCatalogoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "O ajuste do tipo registra a classificação recomendada, a justificativa e quem decidiu")]
    public async Task Handle_RecomendaAjusteDoTipo()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "RECOMENDAR_AJUSTE", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, "SENSIVEL", "Traz dado de saúde."));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        laudo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.AjusteRecomendado,
            Justificativa = "Traz dado de saúde.",
            ClassificacaoRecomendada = ClassificacaoDeProtecao.Sensivel,
            DecididaPor = Isabella,
            DecididaEm = Agora,
        });
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A reprovação do fato grava a justificativa aparada")]
    public async Task Handle_ReprovaOFato()
    {
        FatoCandidato fato = NovoFato();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "FATO", fato.Id, "REPROVAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, Justificativa: "  Dado desnecessário.  "));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Reprovada);
        fato.Aprovacao.Justificativa.Should().Be("Dado desnecessário.");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O ajuste sem justificativa, ou com ela em branco, é aceito e não guarda justificativa")]
    public async Task Handle_AjusteSemJustificativa_Aceito()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", laudo.Id, "RECOMENDAR_AJUSTE", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, "SENSIVEL", "   "));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        laudo.Aprovacao.Justificativa.Should().BeNull();
    }

    [Theory(DisplayName = "A decisão com recomendação ou justificativa que não cabe, ou a reprovação sem justificativa, é recusada no campo antes de ler o item")]
    [InlineData("APROVAR", "SENSIVEL", null, "classificacaoRecomendada", "DadoDaDecisaoNaoCabe")]
    [InlineData("APROVAR", null, "Motivo.", "justificativa", "DadoDaDecisaoNaoCabe")]
    [InlineData("REPROVAR", "SENSIVEL", "Motivo.", "classificacaoRecomendada", "DadoDaDecisaoNaoCabe")]
    [InlineData("REPROVAR", null, null, "justificativa", "JustificativaObrigatoria")]
    [InlineData("REPROVAR", null, "   ", "justificativa", "JustificativaObrigatoria")]
    public async Task Handle_DadoDaDecisaoInvalido_RecusaNoCampo(
        string decisao, string? recomendada, string? justificativa, string campo, string codigo)
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", Guid.NewGuid(), decisao, "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, recomendada, justificativa));

        resultado.Errors.Should().ContainSingle(e => e.Field == campo).Which.Error.Code.Should().Be($"ProtecaoDoCatalogo.{codigo}");
        await _tipos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Theory(DisplayName = "A justificativa de até 2.000 caracteres é aceita; com mais, recusada")]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public async Task Handle_LimiteDaJustificativa(int tamanho, bool aceita)
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", laudo.Id, "REPROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, Justificativa: new string('a', tamanho)));

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Errors.Should().ContainSingle(e => e.Field == "justificativa")
                .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.JustificativaMuitoLonga);
        }
    }

    [Fact(DisplayName = "A aprovação do fato passa pela mutação do fato e grava")]
    public async Task Handle_AprovaOFato()
    {
        FatoCandidato fato = NovoFato();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("FATO", fato.Id, "APROVAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O cadastro e a decisão fora do vocabulário são recusados juntos, cada um no seu campo, sem ler nada")]
    public async Task Handle_FormaInvalida_AcumulaRecusas()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("MODALIDADE", Guid.NewGuid(), null, "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("cadastro", ProtecaoDoCatalogoErrorCodes.CadastroInvalido),
            ("decisao", ProtecaoDoCatalogoErrorCodes.DecisaoInvalida),
        ]);
        await _tipos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "A decisão sobre o tipo que não está em solicitação é recusada e nada é gravado")]
    public async Task Handle_TipoForaDeSolicitacao_Recusa()
    {
        TipoDocumento pendente = NovoTipo();
        _tipos.ObterPorIdAsync(pendente.Id, Arg.Any<CancellationToken>()).Returns(pendente);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", pendente.Id, "APROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão sobre o tipo alterado concorrentemente devolve conflito e descarta o que não foi salvo")]
    public async Task Handle_TipoAlteradoConcorrentemente_DevolveConflito()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new DbUpdateConcurrencyException()));

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "APROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A decisão sobre a proteção que o aprovador conferiu e que o item deixou de ter responde conflito, sem decidir")]
    public async Task Handle_ProtecaoMudouDepoisDeConferida_DevolveConflito()
    {
        TipoDocumento laudo = TipoSolicitado();
        FatoCandidato fato = NovoFato();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        // O aprovador viu outra classificação para o tipo e outra finalidade para o fato.
        Result tipo = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "APROVAR", "SENSIVEL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));
        Result outraFinalidade = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("FATO", fato.Id, "APROVAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Outra finalidade."));

        tipo.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou);
        outraFinalidade.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão sem a proteção conferida é recusada no campo")]
    public async Task Handle_SemProtecaoConferida_Recusa()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", Guid.NewGuid(), "APROVAR", null, null, null));

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("classificacaoProtecao", "hipoteseLegal", "finalidadeTratamento");
    }

    [Fact(DisplayName = "A classificação ou a hipótese conferida fora do vocabulário é recusada no campo, e não vira conflito")]
    public async Task Handle_TokenConferidoInvalido_RecusaNoCampo()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", Guid.NewGuid(), "APROVAR", "SECRETISSIMO", "NAO_EXISTE", Finalidade));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("classificacaoProtecao", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
            ("hipoteseLegal", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
        ]);
    }

    [Theory(DisplayName = "O ajuste sem a classificação recomendada, ou com uma fora do vocabulário, é recusada no campo antes de ler o item")]
    [InlineData(null)]
    [InlineData("SECRETISSIMO")]
    public async Task Handle_AjusteSemRecomendacaoValida_RecusaAntesDeLerOItem(string? recomendada)
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", Guid.NewGuid(), "RECOMENDAR_AJUSTE", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, recomendada));

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);
        await _tipos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "O tipo inexistente é 404 do cadastro de tipos")]
    public async Task Handle_TipoInexistente_NaoEncontrado()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", Guid.NewGuid(), "APROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O ajuste registra o ato com a proteção conferida, a classificação recomendada, a justificativa e quem decidiu")]
    public async Task Handle_RegistraOAjuste()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "RECOMENDAR_AJUSTE", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, "SENSIVEL", " Traz dado de saúde. "));

        _atos.Received(1).Adicionar(Arg.Is<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.AjusteRecomendado
            && a.Cadastro == CadastroDoCatalogo.TipoDocumento
            && a.ItemId == laudo.Id
            && a.ItemNome == "Laudo médico complementar"
            && a.ClassificacaoProtecao == ClassificacaoDeProtecao.Interno
            && a.HipoteseLegal == HipoteseLegalTratamento.CumprimentoObrigacaoLegal
            && a.FinalidadeTratamento == Finalidade
            && a.ClassificacaoRecomendada == ClassificacaoDeProtecao.Sensivel
            && a.Justificativa == "Traz dado de saúde."
            && a.AtorEmissor == Emissor
            && a.AtorSub == Isabella
            && a.AtorNome == "Isabella Figueiredo"
            && a.Instante == Agora));
    }

    [Fact(DisplayName = "A reprovação do fato registra o ato com a justificativa e sem classificação recomendada")]
    public async Task Handle_RegistraAReprovacao()
    {
        FatoCandidato fato = NovoFato();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "FATO", fato.Id, "REPROVAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, Justificativa: "Dado desnecessário."));

        _atos.Received(1).Adicionar(Arg.Is<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.Reprovacao
            && a.Cadastro == CadastroDoCatalogo.Fato
            && a.ItemId == fato.Id
            && a.ClassificacaoProtecao == ClassificacaoDeProtecao.Pessoal
            && a.ClassificacaoRecomendada == null
            && a.Justificativa == "Dado desnecessário."));
    }

    [Fact(DisplayName = "A aprovação registra o ato sem classificação recomendada nem justificativa")]
    public async Task Handle_RegistraAAprovacao()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "APROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        _atos.Received(1).Adicionar(Arg.Is<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.Aprovacao && a.ClassificacaoRecomendada == null && a.Justificativa == null));
    }

    [Fact(DisplayName = "A decisão que o circuito recusa, ou sobre a proteção que mudou, não registra ato")]
    public async Task Handle_Recusada_NaoRegistraAto()
    {
        TipoDocumento naoSolicitado = NovoTipo();
        _tipos.ObterPorIdAsync(naoSolicitado.Id, Arg.Any<CancellationToken>()).Returns(naoSolicitado);
        TipoDocumento editado = TipoSolicitado();
        _tipos.ObterPorIdAsync(editado.Id, Arg.Any<CancellationToken>()).Returns(editado);

        await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", naoSolicitado.Id, "APROVAR", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));
        await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", editado.Id, "APROVAR", "SENSIVEL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
    }

    private static TipoDocumento TipoSolicitado()
    {
        TipoDocumento tipo = NovoTipo();
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        return tipo;
    }

    private Task<Result> DecidirAsync(DecidirProtecaoDoCatalogoCommand command) =>
        DecidirProtecaoDoCatalogoCommandHandler.Handle(command, _tipos, _fatos, _atos, _unitOfWork, UsuarioIsabella(), Relogio(), CancellationToken.None);
}

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
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "A negativa do tipo registra a classificação recomendada e quem negou")]
    public async Task Handle_NegaOTipoComRecomendacao()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "NEGADA", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, "SENSIVEL"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        laudo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Negada,
            ClassificacaoRecomendada = ClassificacaoDeProtecao.Sensivel,
            DecididaPor = Isabella,
            DecididaEm = Agora,
        });
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A aprovação do fato passa pela mutação do fato e grava")]
    public async Task Handle_AprovaOFato()
    {
        FatoCandidato fato = NovoFato();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("FATO", fato.Id, "APROVADA", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

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

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", pendente.Id, "APROVADA", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão sobre o tipo alterado concorrentemente devolve conflito e descarta o que não foi salvo")]
    public async Task Handle_TipoAlteradoConcorrentemente_DevolveConflito()
    {
        TipoDocumento laudo = TipoSolicitado();
        _tipos.ObterPorIdAsync(laudo.Id, Arg.Any<CancellationToken>()).Returns(laudo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new DbUpdateConcurrencyException()));

        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "APROVADA", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

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
        Result tipo = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", laudo.Id, "APROVADA", "SENSIVEL", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));
        Result outraFinalidade = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("FATO", fato.Id, "APROVADA", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Outra finalidade."));

        tipo.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou);
        outraFinalidade.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão sem a proteção conferida é recusada no campo")]
    public async Task Handle_SemProtecaoConferida_Recusa()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", Guid.NewGuid(), "APROVADA", null, null, null));

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("classificacaoProtecao", "hipoteseLegal", "finalidadeTratamento");
    }

    [Fact(DisplayName = "A classificação ou a hipótese conferida fora do vocabulário é recusada no campo, e não vira conflito")]
    public async Task Handle_TokenConferidoInvalido_RecusaNoCampo()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", Guid.NewGuid(), "APROVADA", "SECRETISSIMO", "NAO_EXISTE", Finalidade));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("classificacaoProtecao", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
            ("hipoteseLegal", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
        ]);
    }

    [Theory(DisplayName = "A negativa sem a classificação recomendada, ou com uma fora do vocabulário, é recusada no campo antes de ler o item")]
    [InlineData(null)]
    [InlineData("SECRETISSIMO")]
    public async Task Handle_NegativaSemRecomendacaoValida_RecusaAntesDeLerOItem(string? recomendada)
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand(
            "TIPO_DOCUMENTO", Guid.NewGuid(), "NEGADA", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, recomendada));

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);
        await _tipos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "O tipo inexistente é 404 do cadastro de tipos")]
    public async Task Handle_TipoInexistente_NaoEncontrado()
    {
        Result resultado = await DecidirAsync(new DecidirProtecaoDoCatalogoCommand("TIPO_DOCUMENTO", Guid.NewGuid(), "APROVADA", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade));

        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
    }

    private static TipoDocumento TipoSolicitado()
    {
        TipoDocumento tipo = NovoTipo();
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        return tipo;
    }

    private Task<Result> DecidirAsync(DecidirProtecaoDoCatalogoCommand command) =>
        DecidirProtecaoDoCatalogoCommandHandler.Handle(command, _tipos, _fatos, _unitOfWork, UsuarioIsabella(), Relogio(), CancellationToken.None);
}

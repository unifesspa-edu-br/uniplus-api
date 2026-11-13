namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// A decisão do Representante do controlador sobre o tipo de documento e o fato que estão numa solicitação (ADR-0153),
/// diante das manifestações técnicas vigentes (UNI-REQ-0374).
/// </summary>
public sealed class DecisaoNaProtecaoDoCatalogoTests
{
    private const string Justificativa = "A classificação interna basta: o laudo só comprova a matrícula.";

    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    // O que o Representante do controlador leu do circuito do último item montado no cenário.
    private (int Ciclo, int Manifestacoes) _lida = (1, 0);

    [Fact(DisplayName = "O ajuste do tipo muda o circuito e registra o ato com a recomendação, a justificativa e quem decidiu")]
    public async Task Ajuste_RegistraOAto()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado());

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "RECOMENDAR_AJUSTE", "SENSIVEL", " Traz dado de saúde. ");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (laudo.Aprovacao.Situacao, laudo.Aprovacao.Justificativa, laudo.Aprovacao.DecididaPor)
            .Should().Be((SituacaoDaAprovacao.AjusteRecomendado, "Traz dado de saúde.", Isabella));
        AtoRegistrado().Should().Match<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.AjusteRecomendado && a.ClassificacaoRecomendada == ClassificacaoDeProtecao.Sensivel
            && a.Justificativa == "Traz dado de saúde." && a.AtorSub == Isabella && a.Instante == Agora && !a.DianteDeManifestacaoDesfavoravel);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A aprovação sem manifestação aceita a justificativa opcional e a registra só no ato")]
    public async Task Aprovacao_ComJustificativa_FicaNoAto()
    {
        FatoCandidato fato = Fato(new ItemSobAprovacaoBuilder().Solicitado());

        Result resultado = await DecidirAsync("FATO", fato.Id, "APROVAR", justificativa: Justificativa, classificacao: "PESSOAL");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (fato.Aprovacao.Situacao, fato.Aprovacao.Justificativa).Should().Be((SituacaoDaAprovacao.Aprovada, (string?)null));
        AtoRegistrado().Justificativa.Should().Be(Justificativa);
    }

    [Theory(DisplayName = "Aprovar ou recomendar ajuste sem justificativa diante de manifestação desfavorável é recusado sem mudar nem gravar")]
    [InlineData("APROVAR", null)]
    [InlineData("RECOMENDAR_AJUSTE", "SENSIVEL")]
    public async Task DianteDeDesfavoravel_SemJustificativa_Recusa(string decisao, string? recomendada)
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA"));

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, decisao, recomendada);

        resultado.Errors.Should().ContainSingle(e => e.Field == "justificativa")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "O ajuste sem justificativa, diante de manifestação desfavorável e com a classificação atual, sai com as duas recusas, sem mudar nem gravar")]
    public async Task DianteDeDesfavoravel_AjusteIgualAAtual_AcumulaAsDuasRecusas()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA"));

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "RECOMENDAR_AJUSTE", "INTERNO");

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao, ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Com justificativa, a aprovação diante de manifestação desfavorável é gravada e o ato o registra")]
    public async Task DianteDeDesfavoravel_ComJustificativa_Aprova()
    {
        FatoCandidato fato = Fato(new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("RECOMENDA_AJUSTE"));

        Result resultado = await DecidirAsync("FATO", fato.Id, "APROVAR", justificativa: Justificativa, classificacao: "PESSOAL");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        AtoRegistrado().Should().Match<AtoDaProtecaoDoCatalogo>(a =>
            a.Ato == AtoDaProtecao.Aprovacao && a.Justificativa == Justificativa && a.DianteDeManifestacaoDesfavoravel && a.Ciclo == CicloDaAprovacao.Primeiro);
    }

    [Fact(DisplayName = "A manifestação desfavorável substituída por adequada deixa de exigir a justificativa")]
    public async Task DesfavoravelSubstituida_DispensaAJustificativa()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA").ComManifestacao("ADEQUADA", texto: null));

        (await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "APROVAR")).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A reprovação do fato grava a justificativa aparada")]
    public async Task Reprovacao_DoFato()
    {
        FatoCandidato fato = Fato(new ItemSobAprovacaoBuilder().Solicitado());

        Result resultado = await DecidirAsync("FATO", fato.Id, "REPROVAR", justificativa: "  Dado desnecessário.  ", classificacao: "PESSOAL");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (fato.Aprovacao.Situacao, fato.Aprovacao.Justificativa).Should().Be((SituacaoDaAprovacao.Reprovada, "Dado desnecessário."));
        AtoRegistrado().Should().Match<AtoDaProtecaoDoCatalogo>(a => a.Ato == AtoDaProtecao.Reprovacao && a.Cadastro == CadastroDoCatalogo.Fato);
    }

    [Fact(DisplayName = "A decisão sobre o item fora de solicitação é recusada pelo circuito e não registra ato")]
    public async Task ForaDeSolicitacao_Recusa()
    {
        TipoDocumento pendente = Tipo(new ItemSobAprovacaoBuilder());

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", pendente.Id, "APROVAR");

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "A decisão sobre a proteção que o item deixou de ter responde conflito, sem decidir")]
    public async Task ProtecaoMudou_Recusa()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado());

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "APROVAR", classificacao: "SENSIVEL");

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ProtecaoMudou);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Theory(DisplayName = "A corrida perdida no tipo e no fato responde o mesmo conflito e descarta o que não foi salvo")]
    [InlineData("TIPO_DOCUMENTO", "INTERNO")]
    [InlineData("FATO", "PESSOAL")]
    public async Task Concorrencia_MesmoConflito(string cadastro, string classificacao)
    {
        Guid id = cadastro == "FATO" ? Fato(new ItemSobAprovacaoBuilder().Solicitado()).Id : Tipo(new ItemSobAprovacaoBuilder().Solicitado()).Id;
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new DbUpdateConcurrencyException()));

        Result resultado = await DecidirAsync(cadastro, id, "APROVAR", classificacao: classificacao);

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A manifestação registrada depois da leitura recusa a decisão por conflito, sem mudar nem gravar")]
    public async Task ManifestacaoDepoisDaLeitura_Recusa()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado();
        TipoDocumento laudo = Tipo(montagem.ComManifestacao("ADEQUADA", texto: null));
        _lida = (1, 0);

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "APROVAR");

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida);
        laudo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "A proteção editada e a manifestação registrada depois da leitura saem juntas no lote de conflitos")]
    public async Task ProtecaoEManifestacaoDepoisDaLeitura_AcumulaOsConflitos()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA"));
        _lida = (1, 0);

        Result resultado = await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "APROVAR", classificacao: "SENSIVEL");

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(
            ProtecaoDoCatalogoErrorCodes.ProtecaoMudou, ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida);
        _atos.DidNotReceiveWithAnyArgs().Adicionar(default!);
    }

    [Fact(DisplayName = "O ciclo lido diferente do corrente recusa a decisão como manifestação não lida")]
    public async Task CicloDiferenteDoLido_Recusa()
    {
        TipoDocumento laudo = Tipo(new ItemSobAprovacaoBuilder().Solicitado());
        _lida = (2, 0);

        (await DecidirAsync("TIPO_DOCUMENTO", laudo.Id, "APROVAR")).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida);
    }

    [Fact(DisplayName = "O item inexistente responde o não encontrado do cadastro; a forma inválida não lê nada")]
    public async Task Inexistente_EFormaInvalida()
    {
        (await DecidirAsync("TIPO_DOCUMENTO", Guid.NewGuid(), "APROVAR")).Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
        (await DecidirAsync("FATO", Guid.NewGuid(), "APROVAR")).Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaoEncontrado);
        (await DecidirAsync("MODALIDADE", Guid.NewGuid(), "APROVAR")).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
        await _fatos.Received(1).ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private TipoDocumento Tipo(ItemSobAprovacaoBuilder montagem)
    {
        TipoDocumento tipo = montagem.Tipo();
        _tipos.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        _lida = (tipo.Aprovacao.Ciclo.Numero, tipo.Aprovacao.ManifestacoesNoCiclo);
        return tipo;
    }

    private FatoCandidato Fato(ItemSobAprovacaoBuilder montagem)
    {
        FatoCandidato fato = montagem.Fato();
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _lida = (fato.Aprovacao.Ciclo.Numero, fato.Aprovacao.ManifestacoesNoCiclo);
        return fato;
    }

    private AtoDaProtecaoDoCatalogo AtoRegistrado() =>
        (AtoDaProtecaoDoCatalogo)_atos.ReceivedCalls().Should().ContainSingle().Which.GetArguments()[0]!;

    private Task<Result> DecidirAsync(
        string cadastro, Guid id, string decisao, string? recomendada = null, string? justificativa = null, string classificacao = "INTERNO") =>
        DecidirProtecaoDoCatalogoCommandHandler.Handle(
            new DecidirProtecaoDoCatalogoCommand(
                cadastro, id, decisao, classificacao, "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, _lida.Ciclo, _lida.Manifestacoes, recomendada, justificativa),
            new DecisaoNaProtecaoDoCatalogo(new ItensSobAprovacao(_tipos, _fatos), _atos, _unitOfWork, Relogio()),
            UsuarioIsabella(),
            CancellationToken.None);
}

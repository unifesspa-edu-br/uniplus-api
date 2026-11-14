namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// A solicitação avisa também quem tem a permissão de se manifestar (UNI-REQ-0373): um e-mail por pessoa e por
/// solicitação, sem nome de pessoa; a falta de quem possa se manifestar não impede a solicitação.
/// </summary>
public sealed class SolicitacaoAvisaQuemSeManifestaTests
{
    private const string Kevin = "kevinandersoncarvalho@unifesspa.edu.br";
    private const string Lucia = "luciabrunaalmeida@unifesspa.edu.br";

    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Cada titular da permissão de se manifestar recebe um único e-mail da solicitação, sem nome de pessoa")]
    public async Task Solicitar_AvisaCadaQuemSeManifestaUmaVez()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await SolicitarAsync([Caroline], [Kevin, Lucia]);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        EnviarEmail[] aQuemSeManifesta = [.. mensagens.OfType<EnviarEmail>().Where(static e => e.Finalidade == "catalogo.protecao.solicitacao.manifestacao")];
        aQuemSeManifesta.Select(static e => e.Destinatario).Should().BeEquivalentTo(Kevin, Lucia);
        aQuemSeManifesta.Should().OnlyContain(e => e.CorpoTexto.Contains("1 tipo de documento") && !e.CorpoTexto.Contains(Isabella) && !e.CorpoTexto.Contains("Laudo"));
    }

    [Fact(DisplayName = "Sem ninguém com a permissão de se manifestar a solicitação segue, com o aviso só a quem aprova")]
    public async Task Solicitar_SemQuemSeManifeste_NaoRecusa()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await SolicitarAsync([Caroline], []);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>().Which.Destinatario.Should().Be(Caroline);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Quem já é avisado como aprovador não recebe o segundo e-mail como quem se manifesta")]
    public async Task Solicitar_QuemAprovaENaoRecebeDoisEmails()
    {
        (_, IEnumerable<object> mensagens) = await SolicitarAsync([Caroline], [Caroline.ToUpperInvariant(), Kevin]);

        mensagens.OfType<EnviarEmail>().Select(static e => e.Destinatario).Should().BeEquivalentTo(Caroline, Kevin);
    }

    [Fact(DisplayName = "A solicitação recusada por falta de quem aprove não avisa quem se manifesta")]
    public async Task Solicitar_SemQuemAprove_NaoAvisaNinguem()
    {
        (Result<SolicitacaoDaProtecaoDto> resposta, IEnumerable<object> mensagens) = await SolicitarAsync([], [Kevin]);

        resposta.IsFailure.Should().BeTrue();
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os avisos da solicitação nomeiam o Representante do controlador e o Encarregado de dados e qualificam o catálogo")]
    public async Task Solicitar_TextosNomeiamOsPapeis()
    {
        (_, IEnumerable<object> mensagens) = await SolicitarAsync([Caroline], [Kevin]);

        EnviarEmail[] avisos = [.. mensagens.OfType<EnviarEmail>()];
        EnviarEmail aQuemDecide = avisos.Single(static e => e.Finalidade == "catalogo.protecao.solicitacao");
        EnviarEmail aQuemSeManifesta = avisos.Single(static e => e.Finalidade == "catalogo.protecao.solicitacao.manifestacao");
        aQuemDecide.Assunto.Should().Contain("aguardam a decisão do Representante do controlador");
        aQuemSeManifesta.Assunto.Should().Contain("manifestação do Encarregado de dados");
        aQuemSeManifesta.CorpoTexto.Should().Contain("a decisão cabe ao Representante do controlador");
        avisos.Should().OnlyContain(static e => CatalogoQualificado(e.Assunto) && CatalogoQualificado(e.CorpoTexto) && CatalogoQualificado(e.CorpoHtml));
    }

    // O texto fala do catálogo ao menos uma vez, e cada "catálogo" vem seguido do que ele cataloga.
    internal static bool CatalogoQualificado(string texto)
    {
        string[] depoisDeCadaCatalogo = [.. texto.Split("catálogo").Skip(1)];

        return depoisDeCadaCatalogo.Length > 0
            && depoisDeCadaCatalogo.All(static resto => resto.StartsWith(" de tipos de documento e de fatos do candidato", StringComparison.Ordinal));
    }

    private Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> SolicitarAsync(
        IReadOnlyList<string> aprovadores, IReadOnlyList<string> quemSeManifesta)
    {
        TipoDocumento laudo = NovoTipo();
        _tipos.ListarPendentesPorIdAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([laudo]);
        ITitularesDaPermissao titulares = Substitute.For<ITitularesDaPermissao>();
        titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, Arg.Any<CancellationToken>()).Returns(aprovadores);
        titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoManifestar.Codigo, Arg.Any<CancellationToken>()).Returns(quemSeManifesta);
        SolicitacaoDaProtecaoDoCatalogo solicitacao = new(_tipos, _fatos, Substitute.For<IAtoDaProtecaoDoCatalogoRepository>(), _unitOfWork, titulares, Relogio());

        return SolicitarAprovacaoDaProtecaoCommandHandler.Handle(
            new SolicitarAprovacaoDaProtecaoCommand([new ItemParaSolicitarDto("TIPO_DOCUMENTO", laudo.Id)]),
            solicitacao,
            VerificadorCom(PermissaoConfiguracao.TiposDocumentoManter),
            UsuarioIsabella(),
            CancellationToken.None);
    }
}

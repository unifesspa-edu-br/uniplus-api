namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A publicação do ato avulso: cada recusa da conferência tem erro nomeado e acontece antes de
/// qualquer escrita, e o ato aceito grava o registro e enfileira a requisição de registro.
/// </summary>
public sealed class PublicarAtoAvulsoDoProcessoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly DataDoAto = new(2026, 12, 10);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly Guid _versaoDivulgadaAtoId = Guid.CreateVersion7();

    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly ICertameDivulgadoRepository _divulgados = Substitute.For<ICertameDivulgadoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly ITipoAtoPublicadoReader _tipos = Substitute.For<ITipoAtoPublicadoReader>();
    private readonly IVagaDeLinhagemReader _vagas = Substitute.For<IVagaDeLinhagemReader>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    private readonly DocumentoAPublicar _principal = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf", 'a');
    private readonly DocumentoAPublicar _anexo = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Docx, "quadro.docx", 'b');
    private readonly DocumentoAPublicar _anexoPdf = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "tabela.pdf", 'd');
    private readonly DocumentoAPublicar _outroAnexoPdf = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "outro.pdf", 'c');

    public PublicarAtoAvulsoDoProcessoCommandHandlerTests()
    {
        _processos.ObterSiglaDaUnidadeAdministradoraAsync(_processoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        _processos.ObterAtosCriadoresAsync(_processoId, Arg.Any<CancellationToken>()).Returns([_versaoDivulgadaAtoId]);

        _divulgados.ObterParaLeituraAsync(_processoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                _processoId, 1, _versaoDivulgadaAtoId, new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2026", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));
        _atos.ObterComAnexosAsync(_versaoDivulgadaAtoId, Arg.Any<CancellationToken>()).Returns(
            RegistroDoAto(_versaoDivulgadaAtoId, "EDITAL_ABERTURA"));

        Tipo(congela: false, resultado: false);
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1])
                .SelectMany(id => new[] { _principal, _anexo, _anexoPdf, _outroAnexoPdf }.Where(d => d.Id == id)).ToList());
        _atos.ListarDocumentosJaUsadosAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
    }

    [Fact(DisplayName = "O aviso com um anexo grava o registro pendente e enfileira o registro do ato com o hash do principal e a lista de anexos")]
    public async Task Aviso_ComAnexo_GravaORegistroEEnfileiraARequisicao()
    {
        AtoDoProcesso? gravado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(anexos: [new AnexoSolicitadoDoAto(_anexo.Id, "Quadro de vagas")]));

        resposta.IsSuccess.Should().BeTrue();
        gravado.Should().NotBeNull();
        resposta.Value!.AtoId.Should().Be(gravado!.Id);
        gravado.Situacao.Should().Be(SituacaoDoRegistroDoAto.Pendente);
        gravado.UnidadeAdministradora.Should().Be("ceps", "a chave no acervo usa a unidade e o identificador da versão divulgada");
        gravado.IdentificadorLegivelDoProcesso.Should().Be("psm-medicina-2027");
        gravado.DocumentoPrincipalId.Should().Be(_principal.Id);
        gravado.Anexos.Should().ContainSingle().Which.NomeDeExibicao.Should().Be("Quadro de vagas.docx");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());

        RegistrarAtoNormativoRequisicao requisicao = mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>().Subject;
        requisicao.AtoId.Should().Be(gravado.Id);
        requisicao.Orgao.Should().Be("CEPS");
        requisicao.Serie.Should().Be("AVISO");
        requisicao.Ano.Should().Be(2026);
        requisicao.DocumentoHash.Should().Be(_principal.HashSha256);
        requisicao.VersaoInvocadaId.Should().BeNull("o ato avulso não congela configuração");
        requisicao.AtoRetificadoId.Should().BeNull();
        requisicao.Vinculos.Should().ContainSingle().Which.EntidadeId.Should().Be(_processoId);
        requisicao.Anexos.Should().ContainSingle().Which.Should().Be(
            new AnexoDoAtoRequisicao("Quadro de vagas.docx", TiposDeMidiaDeDocumento.Docx, _anexo.HashSha256!));
    }

    [Fact(DisplayName = "A errata que aponta um aviso publicado é aceita com o motivo, e a requisição leva o ato corrigido")]
    public async Task Errata_ApontandoAvisoPublicado_EAceita()
    {
        Guid avisoId = AtoPublicadoDoProcesso(_processoId);
        _atos.ListarCadeiaDeCorrecaoAsync(avisoId, Arg.Any<CancellationToken>()).Returns([avisoId]);

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(atoCorrigidoId: avisoId, motivo: "Data errada no cronograma"));

        resposta.IsSuccess.Should().BeTrue();
        RegistrarAtoNormativoRequisicao requisicao = mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>().Subject;
        requisicao.AtoRetificadoId.Should().Be(avisoId);
        requisicao.MotivoRetificacao.Should().Be("Data errada no cronograma");
    }

    [Fact(DisplayName = "A correção sem motivo é recusada no campo do motivo, antes de travar ou consultar o processo")]
    public async Task CorrecaoSemMotivo_RecusaAntesDeQualquerLeitura()
    {
        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(atoCorrigidoId: Guid.CreateVersion7()));

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "motivoDaCorrecao" && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
        mensagens.Should().BeEmpty();
        await _processos.DidNotReceive().TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await NadaFoiEscrito();
    }

    [Fact(DisplayName = "O motivo sem ato corrigido é recusado no campo do ato corrigido")]
    public async Task MotivoSemAtoCorrigido_Recusa()
    {
        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(motivo: "Data errada"));

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "atoCorrigidoId" && e.Error.Code == AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio);
        await NadaFoiEscrito();
    }

    [Fact(DisplayName = "Processo inexistente é recusado como não encontrado")]
    public async Task ProcessoInexistente_Recusa()
    {
        _processos.ObterSiglaDaUnidadeAdministradoraAsync(_processoId, Arg.Any<CancellationToken>()).Returns((string?)null);

        await ExigirRecusa(Comando(), "ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Processo sem página pública é recusado")]
    public async Task ProcessoSemPaginaPublica_Recusa()
    {
        _divulgados.ObterParaLeituraAsync(_processoId, Arg.Any<CancellationToken>()).Returns((CertameDivulgado?)null);

        await ExigirRecusa(Comando(), AtoDoProcessoErrorCodes.CertameNaoDivulgado);
    }

    [Fact(DisplayName = "Tipo sem versão vigente na data do ato é recusado")]
    public async Task TipoSemVersaoVigente_Recusa()
    {
        _tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        await ExigirRecusa(Comando(), "ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
    }

    [Fact(DisplayName = "Tipo que congela configuração não é documento avulso")]
    public async Task TipoQueCongela_Recusa()
    {
        Tipo(congela: true, resultado: false);

        await ExigirRecusa(Comando(), AtoDoProcessoErrorCodes.TipoCongelaConfiguracao);
    }

    [Fact(DisplayName = "Tipo que é resultado — como convocação e chamada, que determinam a situação do candidato — não é documento avulso")]
    public async Task TipoQueEResultado_Recusa()
    {
        Tipo(congela: false, resultado: true);

        await ExigirRecusa(Comando(), AtoDoProcessoErrorCodes.TipoEhResultado);
    }

    [Fact(DisplayName = "Tipo único por objeto com a vaga tomada por outra linhagem é recusado")]
    public async Task VagaDoObjetoTomada_Recusa()
    {
        _tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns(
            new TipoAtoPublicadoView("AVISO", "Aviso", false, true, false, false));
        _vagas.ObjetoJaTemAtoDeOutraLinhagemAsync(
            EntidadesDeRegistroPeloDominio.ProcessoSeletivo, _processoId, "AVISO", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await ExigirRecusa(Comando(), "ProcessoSeletivo.ObjetoJaTemAtoVivoDoTipo");
    }

    [Fact(DisplayName = "O ato corrigido sem registro na Seleção é recusado")]
    public async Task AtoCorrigidoSemRegistro_Recusa()
    {
        await ExigirRecusa(Comando(atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado);
    }

    [Fact(DisplayName = "O ato corrigido de outro processo seletivo é recusado")]
    public async Task AtoCorrigidoDeOutroProcesso_Recusa()
    {
        Guid deOutroProcesso = AtoPublicadoDoProcesso(Guid.CreateVersion7());

        await ExigirRecusa(Comando(atoCorrigidoId: deOutroProcesso, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoDeOutroProcesso);
    }

    [Fact(DisplayName = "O ato corrigido que congela configuração, como o edital, não é corrigido por documento avulso")]
    public async Task AtoCorrigidoQueCongela_Recusa()
    {
        Guid edital = AtoPublicadoDoProcesso(_processoId, aoCongelar: true);

        await ExigirRecusa(Comando(atoCorrigidoId: edital, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoDeOutraClasse);
    }

    [Fact(DisplayName = "A segunda correção do mesmo ato é recusada quando a primeira ainda está pendente na Seleção")]
    public async Task SegundaCorrecaoComAPrimeiraPendente_Recusa()
    {
        Guid aviso = AtoPublicadoDoProcesso(_processoId);
        _atos.ObterAtoQueCorrigeAsync(aviso, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());

        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(atoCorrigidoId: aviso, motivo: "Erro"));

        resposta.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido);
        resposta.Error.Message.Should().Contain("mais recente da cadeia", "a recusa orienta a corrigir a cabeça da cadeia");
        await NadaFoiEscrito();
    }

    [Fact(DisplayName = "A segunda correção do mesmo ato é recusada quando a primeira já está no registro central")]
    public async Task SegundaCorrecaoComAPrimeiraRegistrada_Recusa()
    {
        Guid aviso = AtoPublicadoDoProcesso(_processoId);
        _vagas.AtoJaFoiRetificadoAsync(aviso, Arg.Any<CancellationToken>()).Returns(true);

        await ExigirRecusa(Comando(atoCorrigidoId: aviso, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido);
    }

    [Fact(DisplayName = "Documento inexistente ou de outro processo é recusado")]
    public async Task DocumentoInexistente_Recusa()
    {
        await ExigirRecusa(Comando(principalId: Guid.CreateVersion7()), AtoDoProcessoErrorCodes.DocumentoNaoEncontrado);
    }

    [Fact(DisplayName = "Documento não confirmado é recusado")]
    public async Task DocumentoNaoConfirmado_Recusa()
    {
        DocumentoAPublicar pendente = DocumentoAPublicar.IniciarPendente(
            _processoId, FinalidadeDoDocumento.DocumentoDeAto, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([pendente]);

        await ExigirRecusa(Comando(principalId: pendente.Id), AtoDoProcessoErrorCodes.DocumentoNaoConfirmado);
    }

    [Fact(DisplayName = "Documento enviado como anexo do edital não serve a um ato")]
    public async Task DocumentoDeOutraFinalidade_Recusa()
    {
        DocumentoAPublicar doEdital = Documento(FinalidadeDoDocumento.AnexoDoEdital, TiposDeMidiaDeDocumento.Pdf, "quadro.pdf", 'c');
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([doEdital]);

        await ExigirRecusa(Comando(principalId: doEdital.Id), AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto);
    }

    [Fact(DisplayName = "O documento principal que não é PDF é recusado")]
    public async Task PrincipalQueNaoEPdf_Recusa()
    {
        DocumentoAPublicar docx = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Docx, "aviso.docx", 'c');
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([docx]);

        await ExigirRecusa(Comando(principalId: docx.Id), AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf);
    }

    [Fact(DisplayName = "O mesmo documento como principal e anexo é recusado")]
    public async Task DocumentoRepetido_Recusa()
    {
        await ExigirRecusa(
            Comando(anexos: [new AnexoSolicitadoDoAto(_principal.Id, "Cópia")]), AtoDoProcessoErrorCodes.DocumentoRepetido);
    }

    [Fact(DisplayName = "Documento já usado em outro ato do processo é recusado")]
    public async Task DocumentoJaUsado_Recusa()
    {
        _atos.ListarDocumentosJaUsadosAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { _principal.Id });

        await ExigirRecusa(Comando(), AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto);
    }

    [Theory(DisplayName = "Anexo sem nome, ou com nome que o download não aceita, é recusado")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("pasta/quadro")]
    public async Task AnexoComNomeInvalido_Recusa(string? nome)
    {
        await ExigirRecusa(
            Comando(anexos: [new AnexoSolicitadoDoAto(_anexo.Id, nome)]), AtoDoProcessoErrorCodes.AnexoNomeInvalido);
    }

    [Fact(DisplayName = "Dois anexos de documentos distintos que terminam no mesmo nome de download são recusados antes de gravar")]
    public async Task AnexosComNomeIgualDepoisDaExtensao_Recusa()
    {
        await ExigirRecusa(
            Comando(anexos: [new AnexoSolicitadoDoAto(_anexoPdf.Id, "quadro"), new AnexoSolicitadoDoAto(_outroAnexoPdf.Id, "quadro.pdf")]),
            AtoDoProcessoErrorCodes.AnexoNomeRepetido);
    }

    [Fact(DisplayName = "Mais anexos que o limite do ato é recusado")]
    public async Task AnexosAlemDoLimite_Recusa()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(i => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), $"anexo {i}"))];

        await ExigirRecusa(Comando(anexos: anexos), AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida);
    }

    private async Task ExigirRecusa(PublicarAtoAvulsoDoProcessoCommand comando, string codigoEsperado)
    {
        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(comando);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(codigoEsperado);
        mensagens.Should().BeEmpty("a recusa acontece antes de qualquer envio ao Publicações");
        await NadaFoiEscrito();
    }

    private async Task NadaFoiEscrito()
    {
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Publicar(PublicarAtoAvulsoDoProcessoCommand comando) =>
        PublicarAtoAvulsoDoProcessoCommandHandler.Handle(
            comando, _processos, _divulgados, _atos, _documentos, _tipos, _vagas, _unitOfWork, new RelogioFixo(Agora), CancellationToken.None);

    private PublicarAtoAvulsoDoProcessoCommand Comando(
        Guid? principalId = null,
        IReadOnlyList<AnexoSolicitadoDoAto>? anexos = null,
        Guid? atoCorrigidoId = null,
        string? motivo = null) =>
        new(_processoId, "AVISO", "5/2026", DataDoAto, "Diretor do CEPS", principalId ?? _principal.Id, anexos, atoCorrigidoId, motivo);

    private void Tipo(bool congela, bool resultado) =>
        _tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns(
            new TipoAtoPublicadoView("AVISO", "Aviso", congela, false, false, resultado));

    /// <summary>Um ato do processo dado, registrado na Seleção; o que congela é um dos atos criadores de versão.</summary>
    private Guid AtoPublicadoDoProcesso(Guid processoId, bool aoCongelar = false)
    {
        AtoDoProcesso ato = RegistroDoAto(Guid.CreateVersion7(), "AVISO", processoId);
        _atos.ObterProcessoDoAtoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(processoId);
        if (aoCongelar)
        {
            _processos.ObterAtosCriadoresAsync(_processoId, Arg.Any<CancellationToken>()).Returns([_versaoDivulgadaAtoId, ato.Id]);
        }

        return ato.Id;
    }

    private AtoDoProcesso RegistroDoAto(Guid atoId, string tipo, Guid? processoId = null) => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            atoId, processoId ?? _processoId, tipo, tipo, "1/2026", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
            Guid.CreateVersion7(), []),
        new RelogioFixo(Agora)).Value!;

    private static DocumentoAPublicar Documento(FinalidadeDoDocumento finalidade, string tipoDeMidia, string nome, char hash)
    {
        // O processo do documento é o do teste; o handler pede os documentos pelo processo.
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoDosDocumentos, finalidade, nome, tipoDeMidia, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string(hash, 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static Guid ProcessoDosDocumentos { get; } = Guid.CreateVersion7();
}

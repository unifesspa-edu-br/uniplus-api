namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoAtoAvulso;

/// <summary>
/// As recusas dos documentos do ato avulso: cada documento recusado dá a sua causa, no campo dele, e os demais continuam
/// conferidos.
/// </summary>
public sealed class RecusasDosDocumentosDoAtoAvulsoTests
{
    private readonly CenarioDaPublicacaoDoAtoAvulso _cenario = new();

    [Fact(DisplayName = "Documento principal inexistente ou de outro processo é recusado no campo do principal")]
    public async Task PrincipalInexistente_Recusa()
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(principalId: Guid.CreateVersion7()), AtoDoProcessoErrorCodes.DocumentoNaoEncontrado, "documentoPrincipalId");
    }

    [Fact(DisplayName = "Documento não confirmado é recusado no campo dele")]
    public async Task DocumentoNaoConfirmado_Recusa()
    {
        DocumentoAPublicar pendente = DocumentoAPublicar.IniciarPendente(
            _cenario.ProcessoId, FinalidadeDoDocumento.DocumentoDeAto, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        _cenario.Documentos.ListarDoProcessoAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([pendente]);

        await _cenario.DeveRecusar(_cenario.Comando(principalId: pendente.Id), AtoDoProcessoErrorCodes.DocumentoNaoConfirmado, "documentoPrincipalId");
    }

    [Fact(DisplayName = "Documento enviado como anexo do edital não serve a um ato")]
    public async Task DocumentoDeOutraFinalidade_Recusa()
    {
        DocumentoAPublicar doEdital = Documento(FinalidadeDoDocumento.AnexoDoEdital, TiposDeMidiaDeDocumento.Pdf, "quadro.pdf", 'c');
        _cenario.Documentos.ListarDoProcessoAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([doEdital]);

        await _cenario.DeveRecusar(
            _cenario.Comando(principalId: doEdital.Id), AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto, "documentoPrincipalId");
    }

    [Fact(DisplayName = "O documento principal que não é PDF é recusado")]
    public async Task PrincipalQueNaoEPdf_Recusa()
    {
        DocumentoAPublicar docx = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Docx, "aviso.docx", 'c');
        _cenario.Documentos.ListarDoProcessoAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([docx]);

        await _cenario.DeveRecusar(_cenario.Comando(principalId: docx.Id), AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf, "documentoPrincipalId");
    }

    [Fact(DisplayName = "O mesmo documento como principal e anexo é recusado no campo do anexo, e só uma vez é lido")]
    public async Task DocumentoRepetido_RecusaNoCampoDaOcorrenciaRepetida()
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(anexos: [new AnexoSolicitadoDoAto(_cenario.Principal.Id, "Cópia")]),
            AtoDoProcessoErrorCodes.DocumentoRepetido, "anexos[0].documentoId");

        await _cenario.Documentos.Received(1).ListarDoProcessoAsync(
            _cenario.ProcessoId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cada ocorrência repetida de um anexo dá uma causa, no campo dela")]
    public async Task AnexoRepetidoVariasVezes_DaUmaCausaPorOcorrenciaRepetida()
    {
        AnexoSolicitadoDoAto[] anexos = [new(_cenario.Anexo.Id, "Um"), new(_cenario.Anexo.Id, "Dois"), new(_cenario.AnexoPdf.Id, "Tres"), new(_cenario.Anexo.Id, "Quatro")];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => e.Field).Should().Equal("anexos[1].documentoId", "anexos[3].documentoId");
        resposta.Errors.Should().OnlyContain(e => e.Error.Code == AtoDoProcessoErrorCodes.DocumentoRepetido);
    }

    [Fact(DisplayName = "Documento já usado em outro ato do processo é recusado no campo dele")]
    public async Task DocumentoJaUsado_Recusa()
    {
        _cenario.Atos.ListarDocumentosJaUsadosAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { _cenario.Principal.Id });

        await _cenario.DeveRecusar(_cenario.Comando(), AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto, "documentoPrincipalId");
    }

    [Fact(DisplayName = "Cada documento já usado em outro ato dá a sua causa, no campo dele")]
    public async Task DocumentosJaUsados_DaUmaCausaPorDocumento()
    {
        _cenario.Atos.ListarDocumentosJaUsadosAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { _cenario.Principal.Id, _cenario.AnexoPdf.Id });
        AnexoSolicitadoDoAto[] anexos = [new(_cenario.Anexo.Id, "Quadro"), new(_cenario.AnexoPdf.Id, "Tabela")];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => e.Field).Should().Equal("documentoPrincipalId", "anexos[1].documentoId");
        resposta.Errors.Should().OnlyContain(e => e.Error.Code == AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto);
    }

    [Theory(DisplayName = "Anexo sem nome, ou com nome que o download não aceita, é recusado no campo do nome")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("pasta/quadro")]
    public async Task AnexoComNomeInvalido_Recusa(string? nome)
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(anexos: [new AnexoSolicitadoDoAto(_cenario.Anexo.Id, nome)]), AtoDoProcessoErrorCodes.AnexoNomeInvalido, "anexos[0].nome");
    }

    [Fact(DisplayName = "Dois anexos que terminam no mesmo nome de download são recusados antes de gravar, no campo do segundo")]
    public async Task AnexosComNomeIgualDepoisDaExtensao_RecusaNoCampoDoSegundo()
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(anexos: [new AnexoSolicitadoDoAto(_cenario.AnexoPdf.Id, "quadro"), new AnexoSolicitadoDoAto(_cenario.OutroAnexoPdf.Id, "quadro.pdf")]),
            AtoDoProcessoErrorCodes.AnexoNomeRepetido, "anexos[1].nome");
    }

    [Fact(DisplayName = "O nome inválido e o nome repetido de anexos diferentes saem na mesma resposta, cada um no campo do anexo")]
    public async Task NomeInvalidoENomeRepetido_SaemJuntos()
    {
        AnexoSolicitadoDoAto[] anexos =
        [
            new(_cenario.Anexo.Id, null),
            new(_cenario.AnexoPdf.Id, "quadro"),
            new(_cenario.OutroAnexoPdf.Id, "quadro.pdf"),
        ];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("anexos[0].nome", AtoDoProcessoErrorCodes.AnexoNomeInvalido),
            ("anexos[2].nome", AtoDoProcessoErrorCodes.AnexoNomeRepetido));
    }

    [Fact(DisplayName = "Dois anexos inexistentes dão duas causas, cada uma no campo do anexo, e o principal é conferido")]
    public async Task DoisAnexosInexistentes_DaoDuasCausas()
    {
        AnexoSolicitadoDoAto[] anexos = [new(Guid.CreateVersion7(), "Um"), new(_cenario.Anexo.Id, "Dois"), new(Guid.CreateVersion7(), "Tres")];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => e.Field).Should().Equal("anexos[0].documentoId", "anexos[2].documentoId");
        resposta.Errors.Should().OnlyContain(e => e.Error.Code == AtoDoProcessoErrorCodes.DocumentoNaoEncontrado);
    }

    [Fact(DisplayName = "As causas saem agrupadas por etapa: envio, formato, nomes e uso, e dentro de cada uma pela posição")]
    public async Task Causas_SaemAgrupadasPorEtapa()
    {
        _cenario.Atos.ListarDocumentosJaUsadosAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { _cenario.OutroAnexoPdf.Id });
        AnexoSolicitadoDoAto[] anexos = [new(_cenario.OutroAnexoPdf.Id, "Tabela"), new(_cenario.Anexo.Id, null), new(Guid.CreateVersion7(), "Lista")];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("anexos[2].documentoId", AtoDoProcessoErrorCodes.DocumentoNaoEncontrado),
            ("anexos[1].nome", AtoDoProcessoErrorCodes.AnexoNomeInvalido),
            ("anexos[0].documentoId", AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto));
    }

    [Fact(DisplayName = "Mais anexos que o limite é recusado no campo dos anexos, e nenhum documento é lido")]
    public async Task AnexosAlemDoLimite_RecusaENaoLeDocumento()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(i => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), $"anexo {i}"))];

        await _cenario.DeveRecusar(_cenario.Comando(anexos: anexos), AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida, "anexos");

        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento recusado na forma não é lido, e os demais são lidos e conferidos")]
    public async Task DocumentoRecusadoNaForma_NaoELido()
    {
        AnexoSolicitadoDoAto[] anexos = [new(_cenario.Principal.Id, "Cópia"), new(Guid.CreateVersion7(), "Outro")];

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(anexos: anexos));

        resposta.Errors.Select(e => e.Field).Should().Equal("anexos[0].documentoId", "anexos[1].documentoId");
        await _cenario.Documentos.Received(1).ListarDoProcessoAsync(
            _cenario.ProcessoId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), Arg.Any<CancellationToken>());
    }
}

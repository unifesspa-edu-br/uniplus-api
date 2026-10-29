namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O rótulo e o nome de download que a API informa de cada arquivo: a posição conta dentro da exigência e da ocorrência.</summary>
public sealed class RotuloDosArquivosDoCandidatoTests
{
    private static readonly Guid InscricaoId = Guid.CreateVersion7();

    private readonly IDocumentoDaInscricaoRepository _repositorio = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly List<ApresentacaoDeDocumento> _apresentacoes = [];
    private readonly List<ArquivoDaApresentacao> _arquivos = [];
    private readonly DocumentoExigido _laudo = Exigencia("LAUDO");
    private readonly DocumentoExigido _certidao = Exigencia("CERTIDAO");

    private static DocumentoExigido Exigencia(string codigo) =>
        DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), codigo, codigo, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral, false, null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;

    private ArquivoDaApresentacao Enviar(DocumentoExigido exigencia, string? ocorrencia, int minuto, string hash)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, exigencia, ocorrencia);
        RelogioFixo relogio = new(DateTimeOffset.UnixEpoch.AddMinutes(minuto));
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, InscricaoId, exigencia, "application/pdf", 1024, relogio, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", relogio);
        arquivo.RegistrarVerificado(1024, hash, FormatoPermitido.Pdf);
        _apresentacoes.Add(apresentacao);
        _arquivos.Add(arquivo);
        return arquivo;
    }

    private async Task<IReadOnlyList<ArquivoDaApresentacaoDto>> ParaOCandidato()
    {
        _repositorio.ListarApresentacoesDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns([.. _apresentacoes]);
        _repositorio.ListarArquivosDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns([.. _arquivos]);
        return (await ArquivosDaInscricao.CarregarAsync(_repositorio, InscricaoId, CancellationToken.None)).ParaOCandidato();
    }

    [Fact(DisplayName = "A posição do arquivo conta por exigência e ocorrência, na ordem da confirmação, e o nome de download leva o início do hash")]
    public async Task Posicao_ContaPorExigenciaEOcorrencia()
    {
        ArquivoDaApresentacao segundoDoLaudo = Enviar(_laudo, null, minuto: 5, "bbbbbbbb" + new string('0', 56));
        ArquivoDaApresentacao primeiroDoLaudo = Enviar(_laudo, null, minuto: 1, "aaaaaaaa" + new string('0', 56));
        ArquivoDaApresentacao daCertidao = Enviar(_certidao, null, minuto: 3, "cccccccc" + new string('0', 56));
        ArquivoDaApresentacao daOcorrencia = Enviar(_laudo, "integrante-1", minuto: 4, "dddddddd" + new string('0', 56));

        IReadOnlyList<ArquivoDaApresentacaoDto> dtos = await ParaOCandidato();

        ArquivoDaApresentacaoDto Do(ArquivoDaApresentacao a) => dtos.Single(d => d.ArquivoId == a.Id);
        Do(primeiroDoLaudo).Rotulo.Should().Be("Arquivo 1");
        Do(primeiroDoLaudo).NomeParaDownload.Should().Be("Arquivo 1 - LAUDO - aaaaaaaa.pdf");
        Do(segundoDoLaudo).NomeParaDownload.Should().Be("Arquivo 2 - LAUDO - bbbbbbbb.pdf");
        Do(daCertidao).Rotulo.Should().Be("Arquivo 1", "outra exigência recomeça a contagem");
        Do(daOcorrencia).Rotulo.Should().Be("Arquivo 1", "outra ocorrência da mesma exigência recomeça a contagem");
    }

    [Fact(DisplayName = "O nome do documento no nome de download perde o que o sistema de arquivos recusa, e o hash sai em minúsculas")]
    public async Task NomeDeDownload_NomeSeguro()
    {
        DocumentoExigido perigoso = Exigencia("Histórico: ensino/médio \\ \"cópia\"; final");
        ArquivoDaApresentacao arquivo = Enviar(perigoso, null, minuto: 1, "ABCDEF12" + new string('0', 56));

        ArquivoDaApresentacaoDto dto = (await ParaOCandidato()).Single(d => d.ArquivoId == arquivo.Id);

        dto.NomeParaDownload.Should().Be("Arquivo 1 - Histórico ensino médio cópia final - abcdef12.pdf");
    }

    [Fact(DisplayName = "O nome do documento muito longo é encurtado e o hash curto não derruba a listagem")]
    public async Task NomeDeDownload_NomeLongoEHashCurto()
    {
        DocumentoExigido longo = Exigencia(new string('é', 200));
        ArquivoDaApresentacao arquivo = Enviar(longo, null, minuto: 1, "abc");

        ArquivoDaApresentacaoDto dto = (await ParaOCandidato()).Single(d => d.ArquivoId == arquivo.Id);

        dto.NomeParaDownload.Should().Be($"Arquivo 1 - {new string('é', 60)} - abc.pdf");
    }

    [Fact(DisplayName = "O arquivo ainda não verificado tem rótulo, mas não tem nome de download")]
    public async Task NaoVerificado_SemNomeDeDownload()
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, _laudo, null);
        ArquivoDaApresentacao emVerificacao = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, InscricaoId, _laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        emVerificacao.Confirmar("\"etag\"", TimeProvider.System);
        _apresentacoes.Add(apresentacao);
        _arquivos.Add(emVerificacao);

        ArquivoDaApresentacaoDto dto = (await ParaOCandidato()).Single();

        dto.Rotulo.Should().Be("Arquivo 1");
        dto.NomeParaDownload.Should().BeNull();
    }
}

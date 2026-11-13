namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Arquivos verificados confirmados no mesmo microssegundo saem sempre na mesma ordem, a do identificador, qualquer que
/// seja a ordem em que o banco os devolve.
/// </summary>
public sealed class ArquivosVerificadosNoMesmoInstanteTests
{
    private static readonly Guid InscricaoId = Guid.CreateVersion7();

    [Fact(DisplayName = "Os arquivos verificados no mesmo instante saem na ordem do identificador")]
    public async Task MesmoInstante_OrdemDoIdentificador()
    {
        RelogioFixo relogio = new(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
        DocumentoExigido laudo = Exigencia();
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, laudo, null);
        ArquivoDaApresentacao[] verificados = [Verificado(apresentacao, laudo, relogio), Verificado(apresentacao, laudo, relogio)];

        ArquivosDaInscricao arquivos = await ArquivosDaInscricao.CarregarAsync(
            Repositorio(apresentacao, [.. verificados.OrderByDescending(a => a.Id)]), InscricaoId, CancellationToken.None);

        arquivos.Verificados().Select(a => a.ArquivoId).Should().Equal(verificados.Select(a => a.Id).Order());
    }

    private static ArquivoDaApresentacao Verificado(ApresentacaoDeDocumento apresentacao, DocumentoExigido exigencia, TimeProvider relogio)
    {
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, InscricaoId, exigencia, "application/pdf", 1024, relogio, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", relogio);
        arquivo.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf);
        return arquivo;
    }

    private static IDocumentoDaInscricaoRepository Repositorio(ApresentacaoDeDocumento apresentacao, ArquivoDaApresentacao[] arquivos)
    {
        IDocumentoDaInscricaoRepository repositorio = Substitute.For<IDocumentoDaInscricaoRepository>();
        repositorio.ListarApresentacoesDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns([apresentacao]);
        repositorio.ListarArquivosDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(arquivos);
        return repositorio;
    }

    private static DocumentoExigido Exigencia() =>
        DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral, obrigatorio: true, null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null).Value!;
}

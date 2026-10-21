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
/// O que o envio da inscrição faz com os arquivos enviados (UNI-REQ-0028, UNI-REQ-0168): fixa os das
/// exigências que se aplicam e remove só os das que não se aplicam e nunca constaram de um envio.
/// </summary>
public sealed class DocumentosEnviadosDaInscricaoTests
{
    private static readonly Guid InscricaoId = Guid.CreateVersion7();

    [Fact(DisplayName = "O arquivo de exigência que se aplica passa a constar do envio e não é removido")]
    public async Task ExigenciaQueSeAplica_FixaOArquivo()
    {
        DocumentoExigido laudo = Exigencia();
        Cenario cenario = Cenario.Com(laudo, ocorrencia: null);

        await Conciliar(cenario, (laudo, SituacaoDoDocumento.Pendente, null));

        await cenario.Repositorio.DidNotReceiveWithAnyArgs().AgendarLimpezaAsync(default!, default, default);
        cenario.Arquivo.ConstouDeEnvio.Should().BeTrue();
    }

    [Fact(DisplayName = "O arquivo de exigência que não se aplica é removido, com o objeto devolvido para quem apaga o armazenamento")]
    public async Task ExigenciaQueNaoSeAplica_Remove()
    {
        DocumentoExigido laudo = Exigencia();
        Cenario cenario = Cenario.Com(laudo, ocorrencia: null);

        await Conciliar(cenario, (laudo, SituacaoDoDocumento.NaoExigido, null));

        await cenario.Repositorio.Received(1).RemoverArquivoAsync(cenario.Arquivo, Arg.Any<CancellationToken>());
        await cenario.Repositorio.Received(1).AgendarLimpezaAsync(cenario.Arquivo.ChaveSelada, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await cenario.Repositorio.Received(1).AgendarLimpezaAsync(cenario.Arquivo.ObjectKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O arquivo de exigência que a versão fixada não tem mais é removido")]
    public async Task ExigenciaForaDaVersao_Remove()
    {
        Cenario cenario = Cenario.Com(Exigencia(), ocorrencia: null);

        await Conciliar(cenario);

        await cenario.Repositorio.Received(1).RemoverArquivoAsync(cenario.Arquivo, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O arquivo que já constou de um envio nunca é removido, ainda que a exigência deixe de se aplicar")]
    public async Task ArquivoQueConstouDeEnvio_NuncaERemovido()
    {
        DocumentoExigido laudo = Exigencia();
        Cenario cenario = Cenario.Com(laudo, ocorrencia: null);
        cenario.Arquivo.MarcarComoConstanteDeEnvio();

        await Conciliar(cenario, (laudo, SituacaoDoDocumento.NaoExigido, null));

        await cenario.Repositorio.DidNotReceiveWithAnyArgs().RemoverArquivoAsync(default!, default);
        await cenario.Repositorio.DidNotReceiveWithAnyArgs().AgendarLimpezaAsync(default!, default, default);
    }

    [Fact(DisplayName = "No grupo repetível o arquivo da ocorrência que saiu da lista é removido, e o da que ficou, não")]
    public async Task Ocorrencia_DecideAOArquivo()
    {
        DocumentoExigido renda = Exigencia();
        Cenario cenario = Cenario.Com(renda, ocorrencia: "m2");

        await Conciliar(cenario, (renda, SituacaoDoDocumento.Pendente, "m1"));

        await cenario.Repositorio.Received(1).RemoverArquivoAsync(cenario.Arquivo, Arg.Any<CancellationToken>());
    }

    private static Task Conciliar(
        Cenario cenario, params (DocumentoExigido Documento, SituacaoDoDocumento Situacao, string? Ocorrencia)[] itens)
    {
        ResultadoDaAvaliacaoDaInscricao avaliacao = FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Documentos = new DocumentosDaInscricao(
                [.. itens.Select(static i => new DocumentoAvaliado(i.Documento, i.Situacao, null, i.Ocorrencia, [], null))],
                new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(),
                []),
        };
        return DocumentosEnviadosDaInscricao.ConciliarNoEnvioAsync(cenario.Repositorio, InscricaoId, avaliacao, DateTimeOffset.UnixEpoch, CancellationToken.None);
    }

    private static DocumentoExigido Exigencia() =>
        DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo", "CAT", Aplicabilidade.Geral, obrigatorio: true, null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    private sealed record Cenario(IDocumentoDaInscricaoRepository Repositorio, ArquivoDaApresentacao Arquivo)
    {
        public static Cenario Com(DocumentoExigido exigencia, string? ocorrencia)
        {
            ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, exigencia.Id, ocorrencia);
            ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
                apresentacao.Id, InscricaoId, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            arquivo.Confirmar(1024, new string('a', 64), TimeProvider.System);

            IDocumentoDaInscricaoRepository repositorio = Substitute.For<IDocumentoDaInscricaoRepository>();
            repositorio.ListarApresentacoesDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns([apresentacao]);
            repositorio.ListarArquivosDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns([arquivo]);
            repositorio.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
            return new Cenario(repositorio, arquivo);
        }
    }
}

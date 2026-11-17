namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O resultado vai aos receptores na ordem do registro e o desfecho é o do primeiro que reconhece o arquivo; os que vêm
/// depois não são consultados, e a validação do resultado vem antes de qualquer receptor.
/// </summary>
public sealed class PrimeiroReceptorDoResultadoDaVerificacaoTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly List<string> _consultados = [];

    [Fact(DisplayName = "O primeiro receptor que reconhece o arquivo devolve as mensagens dele e os seguintes não são consultados")]
    public async Task Handle_PrimeiroQueReconhece_DevolveSuasMensagens()
    {
        EnviarEmail mensagem = Mensagem();
        IReceptorDoResultadoDaVerificacao[] receptores =
        [
            new ReceptorDeTeste("primeiro", DesfechoDoRecebimento.NaoReconhecido, _consultados),
            new ReceptorDeTeste("segundo", DesfechoDoRecebimento.Tratado([mensagem]), _consultados),
            new ReceptorDeTeste("terceiro", DesfechoDoRecebimento.Tratado(), _consultados),
        ];

        IEnumerable<EnviarEmail> mensagens = await Despachar(ResultadoVerificado(), receptores);

        mensagens.Should().ContainSingle().Which.Should().BeSameAs(mensagem);
        _consultados.Should().Equal("primeiro", "segundo");
    }

    [Fact(DisplayName = "O receptor que reconhece o arquivo sem mensagens encerra o despacho com a lista vazia")]
    public async Task Handle_ReconheceSemMensagens_DevolveVazioSemConsultarOSeguinte()
    {
        IReceptorDoResultadoDaVerificacao[] receptores =
        [
            new ReceptorDeTeste("primeiro", DesfechoDoRecebimento.Tratado(), _consultados),
            new ReceptorDeTeste("segundo", DesfechoDoRecebimento.Tratado([Mensagem()]), _consultados),
        ];

        IEnumerable<EnviarEmail> mensagens = await Despachar(ResultadoVerificado(), receptores);

        mensagens.Should().BeEmpty();
        _consultados.Should().Equal("primeiro");
    }

    [Fact(DisplayName = "O arquivo que nenhum receptor reconhece é descartado depois de consultar todos, na ordem")]
    public async Task Handle_NenhumReconhece_DescartaConsultandoTodos()
    {
        IReceptorDoResultadoDaVerificacao[] receptores =
        [
            new ReceptorDeTeste("primeiro", DesfechoDoRecebimento.NaoReconhecido, _consultados),
            new ReceptorDeTeste("segundo", DesfechoDoRecebimento.NaoReconhecido, _consultados),
        ];

        IEnumerable<EnviarEmail> mensagens = await Despachar(ResultadoVerificado(), receptores);

        mensagens.Should().BeEmpty();
        _consultados.Should().Equal("primeiro", "segundo");
    }

    [Fact(DisplayName = "O veredicto indefinido é recusado antes de consultar qualquer receptor")]
    public async Task Handle_VeredictoIndefinido_RecusaAntesDosReceptores()
    {
        ResultadoDaVerificacaoDeArquivo resultado = ResultadoVerificado() with { Veredicto = (VeredictoDaVerificacao)99 };
        IReceptorDoResultadoDaVerificacao[] receptores = [new ReceptorDeTeste("primeiro", DesfechoDoRecebimento.Tratado(), _consultados)];

        Func<Task> despacho = () => Despachar(resultado, receptores);

        await despacho.Should().ThrowAsync<ResultadoDaVerificacaoInvalidoException>();
        _consultados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O resultado verificado sem SHA-256 é recusado antes de consultar qualquer receptor")]
    public async Task Handle_VerificadoSemSha_RecusaAntesDosReceptores()
    {
        ResultadoDaVerificacaoDeArquivo resultado = ResultadoVerificado() with { HashSha256 = " " };
        IReceptorDoResultadoDaVerificacao[] receptores = [new ReceptorDeTeste("primeiro", DesfechoDoRecebimento.Tratado(), _consultados)];

        Func<Task> despacho = () => Despachar(resultado, receptores);

        await despacho.Should().ThrowAsync<ResultadoDaVerificacaoInvalidoException>();
        _consultados.Should().BeEmpty();
    }

    private static Task<IEnumerable<EnviarEmail>> Despachar(ResultadoDaVerificacaoDeArquivo resultado, IEnumerable<IReceptorDoResultadoDaVerificacao> receptores) =>
        RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(resultado, receptores, NullLogger.Instance, CancellationToken.None);

    private static ResultadoDaVerificacaoDeArquivo ResultadoVerificado() =>
        new(Guid.NewGuid(), VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, Sha, 1024, FormatoDoArquivo.Pdf);

    private static EnviarEmail Mensagem() =>
        EnviarEmail.Para(Email.Criar("leandro-viana84@uniplus-test.local").Value!, "Assunto", "Texto", "<p>Texto</p>", "teste.mensagem");

    private sealed class ReceptorDeTeste(string nome, DesfechoDoRecebimento desfecho, List<string> consultados) : IReceptorDoResultadoDaVerificacao
    {
        public Task<DesfechoDoRecebimento> ReceberAsync(ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
        {
            consultados.Add(nome);

            return Task.FromResult(desfecho);
        }
    }
}

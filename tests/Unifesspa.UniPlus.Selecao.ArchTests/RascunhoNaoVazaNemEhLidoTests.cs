namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Duas invariantes do rascunho da publicação: ele não é lido por ninguém além das três rotas
/// que existem para ele, e o que ele guarda não chega a log nenhum.
/// </summary>
public sealed class RascunhoNaoVazaNemEhLidoTests
{
    /// <summary>
    /// Os únicos arquivos que podem tocar o repositório do rascunho. Publicação, retificação,
    /// fechamento e descarte entram por <b>apagarem</b> — quem registra ato destrói o rascunho.
    /// </summary>
    private static readonly string[] PodemTocar =
    [
        "SalvarRascunhoDaPublicacaoCommandHandler.cs",
        "DescartarRascunhoDaPublicacaoCommandHandler.cs",
        "ObterRascunhoDaPublicacaoQueryHandler.cs",
        "PublicarProcessoSeletivoCommandHandler.cs",
        "RetificarProcessoSeletivoCommandHandler.cs",
        "FecharRetificacaoCommandHandler.cs",
        "DescartarRetificacaoCommandHandler.cs",
    ];

    [Fact(DisplayName = "Só o caminho do próprio rascunho e os que registram ato tocam o repositório dele")]
    public void NinguemMaisLeORascunho()
    {
        string[] infratores = [.. ArquivosDaApplication()
            .Where(arquivo => File.ReadAllText(arquivo).Contains(nameof(IRascunhoDePublicacaoRepository), StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(nome => !PodemTocar.Contains(nome, StringComparer.Ordinal))
            .OrderBy(nome => nome, StringComparer.Ordinal)!];

        infratores.Should().BeEmpty(
            "o rascunho guarda o bloco do ato, que pertence a Publicações — Seleção o guarda e o "
            + "apaga, e não o consulta para decidir coisa nenhuma. Publicar continua lendo o que o "
            + "operador declara no corpo do comando, nunca o que ficou guardado");
    }

    [Fact(DisplayName = "A publicação não consulta o rascunho para montar o ato")]
    public void PublicarNaoLeORascunhoParaDeclarar()
    {
        // Apagar é a única coisa que a publicação faz com o rascunho. Ler o conteúdo para
        // preencher o ato inverteria o sentido do gesto: declarar é um ato, e o que vale é o que
        // o operador afirma no momento de publicar, não o que sobrou de uma sessão anterior.
        foreach (string handler in new[]
                 {
                     "PublicarProcessoSeletivoCommandHandler.cs",
                     "RetificarProcessoSeletivoCommandHandler.cs",
                     "FecharRetificacaoCommandHandler.cs",
                 })
        {
            string fonte = File.ReadAllText(Path.Join(CaminhoDosComandos(), handler));

            fonte.Should().NotContain("ObterDoOperadorAsync", $"{handler} só apaga o rascunho");
            fonte.Should().NotContain(nameof(RascunhoDePublicacao.Conteudo), $"{handler} não lê o conteúdo do rascunho");
        }
    }

    [Fact(DisplayName = "O caminho do rascunho não tem logger — o conteúdo não chega a log nenhum")]
    public void OConteudoNaoAlcancaLog()
    {
        // O PiiMaskingEnricher mascara por NOME DE PROPRIEDADE, e o conteúdo é um documento
        // opaco: não expõe nenhum. O nome do assinante atravessaria qualquer enricher intacto.
        // Não havendo logger no caminho, não há como ele escapar por ali.
        string[] arquivos = [.. ArquivosDoRascunho()];

        // Sem esta conferência o teste passaria varrendo lista vazia — e um caminho renomeado
        // sairia da cobertura em silêncio.
        arquivos.Should().HaveCountGreaterThan(5, "o caminho do rascunho tem entidade, comandos, query e DTO");

        foreach (string arquivo in arquivos)
        {
            string fonte = File.ReadAllText(arquivo);

            fonte.Should().NotContain("ILogger", $"{Path.GetFileName(arquivo)} não deve logar");
            fonte.Should().NotContain("LoggerMessage", $"{Path.GetFileName(arquivo)} não deve logar");
        }
    }

    [Fact(DisplayName = "A recusa de tamanho não devolve o conteúdo recusado na mensagem")]
    public void AMensagemDeRecusaNaoCarregaOConteudo()
    {
        Result<RascunhoDePublicacao> recusado = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(),
            "sub-1",
            $"\"{new string('a', RascunhoDePublicacao.ConteudoMaxBytes + 1)}\"",
            1,
            DateTimeOffset.UnixEpoch,
            RascunhoDePublicacao.Prazo);

        recusado.IsFailure.Should().BeTrue();
        recusado.Error!.Message.Should().NotContain("aaa",
            "a mensagem de erro vai para o cliente e para a telemetria — devolver o que foi "
            + "recusado levaria junto o que o operador transcreveu");
    }

    private static IEnumerable<string> ArquivosDaApplication() =>
        Directory.EnumerateFiles(RaizDaApplication(), "*.cs", SearchOption.AllDirectories);

    private static IEnumerable<string> ArquivosDoRascunho() =>
        ArquivosDaApplication()
            .Concat(Directory.EnumerateFiles(RaizDoDominio(), "RascunhoDePublicacao.cs", SearchOption.AllDirectories))
            .Where(arquivo => Path.GetFileName(arquivo).Contains("RascunhoDaPublicacao", StringComparison.Ordinal)
                || Path.GetFileName(arquivo).Contains("RascunhoDePublicacao", StringComparison.Ordinal));

    private static string CaminhoDosComandos() =>
        Path.Join(RaizDaApplication(), "Commands", "ProcessosSeletivos");

    private static string RaizDaApplication([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..", "src", "selecao",
            "Unifesspa.UniPlus.Selecao.Application"));

    private static string RaizDoDominio([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..", "src", "selecao",
            "Unifesspa.UniPlus.Selecao.Domain"));
}

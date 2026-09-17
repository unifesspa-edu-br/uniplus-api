namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>Fronteira de tipo do contrato público do certame.</b>
/// </summary>
/// <remarks>
/// <para>
/// A configuração congelada cresce a cada incremento do domínio. Sem esta verificação, o bloco
/// seguinte nasce e alguém, projetando por conveniência, o publica sem que ninguém tenha decidido
/// que ele é público — é o risco de exposição excessiva no nível da propriedade: servir o objeto
/// inteiro e entregar campo interno que nunca se pretendeu expor.
/// </para>
/// <para>
/// Com ela, acrescentar bloco ao envelope <b>quebra a construção</b> até que a pessoa o classifique
/// como público, interno, ou público por outro contrato. A decisão de exposição deixa de depender
/// de revisão humana atenta e vira obrigação verificável.
/// </para>
/// <para>
/// É a contrapartida da projeção por tipos declarados, que já impede o vazamento por omissão. Esta
/// suíte cobre o caso em que alguém contorna a forma.
/// </para>
/// </remarks>
public sealed class FronteiraDeBlocosDoCertameTests
{
    [Fact(DisplayName = "Todo bloco da configuração congelada está classificado como público ou interno")]
    public void EnvelopeDeReferencia_NaoTemBlocoSemClassificacao()
    {
        IReadOnlyCollection<string> naoClassificados = NaoClassificados(ChavesDoEnvelopeDeReferencia());

        naoClassificados.Should().BeEmpty(
            "todo bloco da configuração congelada precisa ser classificado antes de existir — os não classificados são: {0}",
            string.Join(", ", naoClassificados));
    }

    [Fact(DisplayName = "O envelope com os blocos condicionais presentes também está inteiramente classificado")]
    public void EnvelopeComBlocosCondicionais_NaoTemBlocoSemClassificacao()
    {
        // Cascata de remanejamento e bônus regional alternam presença. Sem exercitar a variante em
        // que existem, a verificação não os alcançaria e eles poderiam nascer sem classificação.
        SnapshotCanonico canonico = EnvelopeCanonicoGoldenTests.CanonicalizarReferenciaComCascata();
        IReadOnlyCollection<string> naoClassificados = NaoClassificados(ChavesDe(canonico));

        naoClassificados.Should().BeEmpty(
            "os blocos condicionais também precisam de classificação — os não classificados são: {0}",
            string.Join(", ", naoClassificados));
    }

    [Fact(DisplayName = "Um bloco fictício não classificado faz a verificação falhar, nomeando o bloco")]
    public void BlocoFicticio_QuebraAVerificacao()
    {
        // O teste do próprio teste: uma verificação que não falha quando o envelope ganha bloco não
        // é gate, é decoração.
        List<string> comBlocoNovo = [.. ChavesDoEnvelopeDeReferencia(), "dimensaoAindaNaoDecidida"];

        IReadOnlyCollection<string> naoClassificados = NaoClassificados(comBlocoNovo);

        naoClassificados.Should().ContainSingle().Which.Should().Be("dimensaoAindaNaoDecidida");
    }

    [Fact(DisplayName = "Nenhum bloco é classificado em duas categorias ao mesmo tempo")]
    public void Categorias_NaoSeSobrepoem()
    {
        // Um bloco em "público" e "interno" ao mesmo tempo tornaria a classificação inútil: a
        // verificação passaria, e qual das duas vale ficaria indefinido.
        ClassificacaoDosBlocosDoCertame.Publicados
            .Intersect(ClassificacaoDosBlocosDoCertame.Internos, StringComparer.Ordinal)
            .Should().BeEmpty();

        ClassificacaoDosBlocosDoCertame.Publicados
            .Intersect(ClassificacaoDosBlocosDoCertame.PublicosPorOutroContrato, StringComparer.Ordinal)
            .Should().BeEmpty();

        ClassificacaoDosBlocosDoCertame.Internos
            .Intersect(ClassificacaoDosBlocosDoCertame.PublicosPorOutroContrato, StringComparer.Ordinal)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A classificação não guarda bloco que o envelope deixou de emitir")]
    public void Classificacao_NaoTemBlocoMorto()
    {
        // A lista envelhece nos dois sentidos. Um bloco removido do domínio que sobra aqui faz a
        // classificação descrever um envelope que não existe mais.
        HashSet<string> emitidos = [
            .. ChavesDoEnvelopeDeReferencia(),
            .. ChavesDe(EnvelopeCanonicoGoldenTests.CanonicalizarReferenciaComCascata()),
            // Condicional da retificação: só existe depois que o edital é emendado, e a
            // canonicalização de referência publica pela primeira vez.
            "retificacao",
        ];

        ClassificacaoDosBlocosDoCertame.Classificados.Except(emitidos, StringComparer.Ordinal)
            .Should().BeEmpty("a classificação descreve blocos que o envelope não emite mais");
    }

    [Fact(DisplayName = "A projeção pública lê o envelope canônico REAL, não só o escrito à mão nos testes")]
    public void ProjecaoDoCertame_LeOEnvelopeCanonico()
    {
        // A fronteira acima compara as chaves de TOPO. Renomear uma chave INTERNA de bloco no
        // canonicalizador — o rótulo de um documento exigido, o nome de um recurso de atendimento,
        // a lista de formatos — a mantém verde e faz a leitura pública recusar TODO certame, com
        // defeito detectável só em produção. Projetar o snapshot canônico fecha essa porta.
        JsonObject envelope = (JsonObject)JsonNode.Parse(
            EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;

        Result<CertamePublicadoDto> resultado = ProjecaoDoCertamePublicado.Projetar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('a', 64), envelope);

        resultado.IsSuccess.Should().BeTrue(
            "o contrato público precisa saber ler o envelope que o canonicalizador de fato emite — recusa: {0}",
            resultado.Error?.Message);
    }

    private static IReadOnlyCollection<string> NaoClassificados(IEnumerable<string> chaves) =>
        [.. chaves.Where(chave => !ClassificacaoDosBlocosDoCertame.Classificados.Contains(chave)).Order(StringComparer.Ordinal)];

    private static IReadOnlyCollection<string> ChavesDoEnvelopeDeReferencia() =>
        ChavesDe(EnvelopeCanonicoGoldenTests.CanonicalizarReferencia());

    private static IReadOnlyCollection<string> ChavesDe(SnapshotCanonico canonico)
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(canonico.Bytes)!;
        return [.. envelope.Select(static par => par.Key)];
    }
}

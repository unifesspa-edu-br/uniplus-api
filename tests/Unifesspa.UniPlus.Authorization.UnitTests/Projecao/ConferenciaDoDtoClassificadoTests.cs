namespace Unifesspa.UniPlus.Authorization.UnitTests.Projecao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>A propriedade lida direto da coluna não fica abaixo dela, e o valor derivado declara a transformação (ADR-0153).</summary>
public sealed class ConferenciaDoDtoClassificadoTests
{
    private static readonly Dictionary<string, ClassificacaoDeProtecao> Modelo = new(StringComparer.Ordinal)
    {
        ["selecao.inscricoes.numero_inscricao"] = ClassificacaoDeProtecao.Interno,
        ["selecao.respostas_da_inscricao.valor_cifrado"] = ClassificacaoDeProtecao.Sensivel,
    };

    [Fact(DisplayName = "A propriedade lida direto da coluna e classificada abaixo dela é reprovada, com o tipo e a propriedade nomeados")]
    public void LidaDiretoAbaixoDaFonte_Reprova() =>
        ConferenciaDoDtoClassificado.Conferir([typeof(AbaixoDaFonte)], Modelo)
            .Should().ContainSingle(v => v.Contains("AbaixoDaFonte.Numero") && v.Contains("Interno") && v.Contains("Publico"));

    [Fact(DisplayName = "Na fonte ou acima dela a propriedade passa")]
    public void NaFonteOuAcima_Passa() =>
        ConferenciaDoDtoClassificado.Conferir([typeof(NaFonte)], Modelo).Should().BeEmpty();

    [Fact(DisplayName = "O valor derivado abaixo da fonte passa com a transformação declarada e reprova sem ela")]
    public void ValorDerivado_ExigeATransformacao()
    {
        ConferenciaDoDtoClassificado.Conferir([typeof(DerivadoComTransformacao)], Modelo).Should().BeEmpty();
        ConferenciaDoDtoClassificado.Conferir([typeof(DerivadoSemTransformacao)], Modelo)
            .Should().ContainSingle(v => v.Contains("DerivadoSemTransformacao.Cpf"));
    }

    [Fact(DisplayName = "A fonte que o modelo não conhece e a transformação sem fonte reprovam: falha fechada")]
    public void FonteDesconhecida_Reprova()
    {
        ConferenciaDoDtoClassificado.Conferir([typeof(FonteInexistente)], Modelo).Should().ContainSingle(v => v.Contains("não é coluna do modelo"));
        ConferenciaDoDtoClassificado.Conferir([typeof(TransformacaoSemFonte)], Modelo).Should().ContainSingle(v => v.Contains("sem a fonte"));
    }

    private sealed record AbaixoDaFonte(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Publico, Fonte = "selecao.inscricoes.numero_inscricao")] string Numero);

    private sealed record NaFonte(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Interno, Fonte = "selecao.inscricoes.numero_inscricao")] string Numero,
        [property: ProtegidoComo(ClassificacaoDeProtecao.Sensivel, Fonte = "selecao.respostas_da_inscricao.valor_cifrado")] string Resposta);

    private sealed record DerivadoComTransformacao(
        [property: ProtegidoComo(
            ClassificacaoDeProtecao.Pessoal, Fonte = "selecao.respostas_da_inscricao.valor_cifrado", Transformacao = "mascara-cpf")] string Cpf);

    private sealed record DerivadoSemTransformacao(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Pessoal, Fonte = "selecao.respostas_da_inscricao.valor_cifrado")] string Cpf);

    private sealed record FonteInexistente(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Pessoal, Fonte = "selecao.nada.coluna")] string Valor);

    private sealed record TransformacaoSemFonte(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Pessoal, Transformacao = "mascara-cpf")] string Valor);
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;


using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O protocolo comum das 21 operações de escrita da configuração (ADR-0110 D4/D5, ADR-0125 §5):
/// a verificação de argumento precede a guarda, a guarda precede a regra, a recusa não muda a
/// sessão e o sucesso avança a revisão dela.
/// </summary>
public sealed class ProcessoSeletivoEdicaoTests
{
    public static TheoryData<string> Operacoes => [.. OperacaoDeEdicao.Todas.Select(static o => o.Nome)];

    public static TheoryData<string> OperacoesQueVerificamArgumento =>
        [.. OperacaoDeEdicao.Todas.Where(static o => o.ComArgumentoInvalido is not null).Select(static o => o.Nome)];

    [Fact(DisplayName = "O catálogo cobre as 21 operações do protocolo comum")]
    public void Catalogo_TemAs21Operacoes() => OperacaoDeEdicao.Todas.Should().HaveCount(21);

    [Theory(DisplayName = "Argumento nulo lança antes da guarda, mesmo no processo publicado sem sessão")]
    [MemberData(nameof(OperacoesQueVerificamArgumento))]
    public void ArgumentoInvalido_LancaAntesDaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);
        OperacaoDeEdicao operacao = OperacaoDeEdicao.Por(nome);

        Action chamada = () => operacao.ComArgumentoInvalido!(processo, PrecondicaoIfMatch.Ausente);

        chamada.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Treze operações verificam argumento antes da guarda")]
    public void TrezeOperacoes_VerificamArgumento() =>
        OperacaoDeEdicao.Todas.Count(static o => o.ComArgumentoInvalido is not null).Should().Be(13);

    [Theory(DisplayName = "Processo publicado sem sessão recusa a operação pela guarda")]
    [MemberData(nameof(Operacoes))]
    public void PublicadoSemSessao_RecusaPelaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Theory(DisplayName = "Precondição velha recusa a operação sem avançar a revisão da sessão")]
    [MemberData(nameof(Operacoes))]
    public void PrecondicaoVelha_RecusaSemAvancarARevisao(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        PrecondicaoIfMatch velha = PrecondicaoIfMatch.DeTags(["\"outra-sessao:1\""]);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, velha);

        resultado.IsFailure.Should().BeTrue();
        processo.Rascunho!.Revisao.Should().Be(1);
    }

    [Theory(DisplayName = "Operação bem-sucedida sob sessão avança a revisão em um")]
    [MemberData(nameof(Operacoes))]
    public void SucessoSobSessao_AvancaARevisao(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        PrecondicaoIfMatch atual = PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, atual);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Rascunho!.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Toda operação do protocolo declara a violação da sua regra ou o motivo de não ter regra")]
    public void Catalogo_DeclaraRegraOuAusenciaDela()
    {
        string[] todas = [.. OperacaoDeEdicao.Todas.Select(static o => o.Nome)];

        ViolacaoDeRegra.PorOperacao.Keys.Concat(ViolacaoDeRegra.SemRegra.Keys).Should().BeEquivalentTo(todas);
        ViolacaoDeRegra.PorOperacao.Keys.Intersect(ViolacaoDeRegra.SemRegra.Keys).Should().BeEmpty();
    }

    public static TheoryData<string> OperacoesComRegra => [.. ViolacaoDeRegra.PorOperacao.Keys];

    [Theory(DisplayName = "Entrada que viola a regra, no processo publicado sem sessão, é recusada pela guarda e não pela regra")]
    [MemberData(nameof(OperacoesComRegra))]
    public void ViolacaoSemSessao_RecusaPelaGuarda(string nome)
    {
        ViolacaoDeRegra violacao = ViolacaoDeRegra.PorOperacao[nome];
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _, violacao.Preparar);

        Result resultado = violacao.Chamar(processo, PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Theory(DisplayName = "Entrada que viola a regra, sob sessão com a precondição atual, é recusada pelo código da regra sem mudar a dimensão")]
    [MemberData(nameof(OperacoesComRegra))]
    public void ViolacaoSobSessao_RecusaPelaRegraSemMutacao(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        ViolacaoDeRegra violacao = ViolacaoDeRegra.PorOperacao[nome];
        violacao.Preparar?.Invoke(processo);
        object?[] antes = violacao.Dimensao(processo);
        int revisao = processo.Rascunho!.Revisao;

        Result resultado = violacao.Chamar(processo, PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        resultado.IsFailure.Should().BeTrue();
        (resultado.Errors.Select(static e => e.Error.Code).FirstOrDefault()).Should().Be(violacao.Codigo);
        violacao.Dimensao(processo).Should().Equal(antes);
        processo.Rascunho!.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Em rascunho a operação não exige precondição")]
    public void EmRascunho_NaoExigePrecondicao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result resultado = processo.DefinirCascataRemanejamento(null, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Bônus regional nulo é válido quando o processo declara que não o aplica")]
    public void BonusNulo_EValido()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Fatos coletados sem grupos informados são válidos")]
    public void GruposNulos_SaoValidos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(Unifesspa.UniPlus.Regras.Formularios.FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente, grupos: null)
            .IsSuccess.Should().BeTrue();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O protocolo das 21 operações de escrita da configuração (ADR-0175, ADR-0125 §5): a verificação
/// de argumento precede a guarda, a guarda precede a regra, só o rascunho aceita a escrita e a
/// recusa não muda a dimensão.
/// </summary>
public sealed class ProcessoSeletivoEdicaoTests
{
    public static TheoryData<string> Operacoes => [.. OperacaoDeEdicao.Todas.Select(static o => o.Nome)];

    public static TheoryData<string> OperacoesQueVerificamArgumento =>
        [.. OperacaoDeEdicao.Todas.Where(static o => o.ComArgumentoInvalido is not null).Select(static o => o.Nome)];

    public static TheoryData<string> OperacoesComRegra => [.. ViolacaoDeRegra.PorOperacao.Keys];

    [Fact(DisplayName = "O catálogo cobre as 21 operações do protocolo")]
    public void Catalogo_TemAs21Operacoes() => OperacaoDeEdicao.Todas.Should().HaveCount(21);

    [Fact(DisplayName = "Treze operações verificam argumento antes da guarda")]
    public void TrezeOperacoes_VerificamArgumento() =>
        OperacaoDeEdicao.Todas.Count(static o => o.ComArgumentoInvalido is not null).Should().Be(13);

    [Theory(DisplayName = "Argumento nulo lança antes da guarda, mesmo no processo publicado")]
    [MemberData(nameof(OperacoesQueVerificamArgumento))]
    public void ArgumentoInvalido_LancaAntesDaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);
        OperacaoDeEdicao operacao = OperacaoDeEdicao.Por(nome);

        Action chamada = () => operacao.ComArgumentoInvalido!(processo);

        chamada.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Processo em rascunho aceita a operação sem cabeçalho condicional")]
    [MemberData(nameof(Operacoes))]
    public void Rascunho_GravaSemPrecondicao(string nome)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Processo publicado recusa a operação com mutacao_pos_publicacao_bloqueada")]
    [MemberData(nameof(Operacoes))]
    public void Publicado_RecusaPelaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Trocar a localidade do processo publicado é recusado e conserva a localidade anterior")]
    public void DefinirLocalidade_Publicado_RecusaEPreservaLocalidade()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);
        processo.Localidade.CodigoIbge.Should().Be("1504208");

        Result resultado = processo.DefinirLocalidade(LocalidadeRegente.Criar("1501402", "Belém", "PA").Value!);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.Localidade.CodigoIbge.Should().Be("1504208");
    }

    [Theory(DisplayName = "Processo encerrado recusa a operação com mutacao_fora_de_estado_editavel")]
    [MemberData(nameof(Operacoes))]
    public void Encerrado_RecusaPelaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Encerrado();

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
    }

    [Fact(DisplayName = "Toda operação do protocolo declara a violação da sua regra ou o motivo de não ter regra")]
    public void Catalogo_DeclaraRegraOuAusenciaDela()
    {
        string[] todas = [.. OperacaoDeEdicao.Todas.Select(static o => o.Nome)];

        ViolacaoDeRegra.PorOperacao.Keys.Concat(ViolacaoDeRegra.SemRegra.Keys).Should().BeEquivalentTo(todas);
        ViolacaoDeRegra.PorOperacao.Keys.Intersect(ViolacaoDeRegra.SemRegra.Keys).Should().BeEmpty();
    }

    [Theory(DisplayName = "Entrada que viola a regra, no processo publicado, é recusada pela guarda e não pela regra")]
    [MemberData(nameof(OperacoesComRegra))]
    public void ViolacaoEmPublicado_RecusaPelaGuarda(string nome)
    {
        ViolacaoDeRegra violacao = ViolacaoDeRegra.PorOperacao[nome];
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _, violacao.Preparar);

        Result resultado = violacao.Chamar(processo);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Theory(DisplayName = "Entrada que viola a regra, em rascunho, é recusada pelo código da regra sem mudar a dimensão")]
    [MemberData(nameof(OperacoesComRegra))]
    public void ViolacaoEmRascunho_RecusaPelaRegraSemMutacao(string nome)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ViolacaoDeRegra violacao = ViolacaoDeRegra.PorOperacao[nome];
        violacao.Preparar?.Invoke(processo);
        object?[] antes = violacao.Dimensao(processo);

        Result resultado = violacao.Chamar(processo);

        resultado.IsFailure.Should().BeTrue();
        (resultado.Errors.Select(static e => e.Error.Code).FirstOrDefault()).Should().Be(violacao.Codigo);
        violacao.Dimensao(processo).Should().Equal(antes);
    }

    [Fact(DisplayName = "FADA nulo é válido quando o processo declara que não o aplica")]
    public void FadaNulo_EValido()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFada(aplica: false, configuracao: null).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Fatos coletados sem grupos informados são válidos")]
    public void GruposNulos_SaoValidos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(PapelDoFormularioDeTeste.Inscricao, [], grupos: null)
            .IsSuccess.Should().BeTrue();
    }
}

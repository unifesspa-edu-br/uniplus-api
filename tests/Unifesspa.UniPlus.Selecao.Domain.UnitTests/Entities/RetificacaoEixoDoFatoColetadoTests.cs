namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Na retificação, o fato que uma versão publicada já coletou não troca de eixo (UNI-REQ-0144): o
/// candidato pode tê-lo respondido em qualquer versão, e a classificação e a elegibilidade leem as
/// respostas juntas. A comparação é com o que a versão publicada congelou, nunca com o catálogo.
/// </summary>
public sealed class RetificacaoEixoDoFatoColetadoTests
{
    private const string Codigo = "RascunhoRetificacao.EixoDoFatoJaColetadoAlterado";

    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly PrecondicaoIfMatch Sessao = PrecondicaoIfMatch.Curinga;

    [Theory(DisplayName = "A retificação recusa trocar o tipo de renderização ou a classificação de item e de campo de grupo já coletados")]
    [InlineData("item", "tipo de renderização")]
    [InlineData("item", "classificação de proteção")]
    [InlineData("campo", "tipo de renderização")]
    [InlineData("campo", "classificação de proteção")]
    public void EixoTrocado_Recusa(string onde, string eixo)
    {
        ProcessoSeletivo processo = Publicado();
        (TipoRenderizacao tipo, string classificacao) = eixo == "tipo de renderização"
            ? (TipoRenderizacao.Numero, "PESSOAL")
            : (TipoRenderizacao.Booleano, "IDENTIFICADOR");
        FatoColetado trocado = onde == "item"
            ? Fato("TEM_RENDA", 0, tipo, classificacao)
            : Campo("MEMBRO_RENDA", 0, tipo, classificacao);

        Result resultado = onde == "item"
            ? processo.DefinirItens([trocado], Sessao, grupos: [Familia(1, Campo("MEMBRO_RENDA", 0))])
            : processo.DefinirItens([Fato("TEM_RENDA", 0)], Sessao, grupos: [Familia(1, trocado)]);

        resultado.Error!.Code.Should().Be(Codigo);
        resultado.Error.Message.Should().Contain(trocado.FatoCodigo).And.Contain(eixo)
            .And.Contain("processo novo", "a recusa orienta a saída");
    }

    [Fact(DisplayName = "A retificação aceita regravar o item com outro rótulo e outra ajuda: não são eixo")]
    public void RotuloEAjuda_NaoSaoEixo_Aceita()
    {
        ProcessoSeletivo processo = Publicado();
        FatoColetado reescrito = FatoColetado.Criar(
            "TEM_RENDA", 0, "Outro rótulo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", ajuda: "Outra ajuda").Value!;

        Result resultado = processo.DefinirItens([reescrito], Sessao, grupos: [Familia(1, Campo("MEMBRO_RENDA", 0))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fato que nenhuma versão publicada coletou entra com qualquer eixo")]
    public void FatoNaoColetado_NaoEAfetado()
    {
        ProcessoSeletivo processo = Publicado();

        Result resultado = processo.DefinirItens(
            [Fato("TEM_RENDA", 0), Fato("NOVO", 1, TipoRenderizacao.Numero, "IDENTIFICADOR")], Sessao, grupos: [Familia(2, Campo("MEMBRO_RENDA", 0))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O metadado e a operação congelados só recusam quando a versão nova os congela diferentes")]
    public void MetadadoEOperacao_ComparaComOQueAVersaoNovaCongela()
    {
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.De(
            [VersoesPublicadasDeTeste.Versao([], [Fato("TEM_RENDA", 0)])],
            [new EixosDeVersaoPublicada(
                new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = new("BOOLEANO", "ESCALAR", "INSCRICAO") },
                new Dictionary<string, string> { ["HA_MEMBRO"] = "EXISTE" })]);
        Dictionary<string, string> operacoes = new() { ["HA_MEMBRO"] = "EXISTE" };

        publicadas.PrimeiroEixoCongeladoTrocado(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = new("BOOLEANO", "ESCALAR", "INSCRICAO") }, operacoes)
            .Should().BeNull("o que a versão nova congela é o que a versão publicada congelou");
        publicadas.PrimeiroEixoCongeladoTrocado(new Dictionary<string, EixosDoMetadadoCongelado>(), operacoes)
            .Should().BeNull("a versão nova que não cita o fato numa condição não o congela: uma edição do catálogo não conta");
        publicadas.PrimeiroEixoCongeladoTrocado(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = new("CATEGORICO", "ESCALAR", "INSCRICAO") }, operacoes)
            .Should().BeEquivalentTo(new { Fato = "TEM_RENDA", Eixo = "domínio" });
        publicadas.PrimeiroEixoCongeladoTrocado(
            new Dictionary<string, EixosDoMetadadoCongelado>(), new Dictionary<string, string> { ["HA_MEMBRO"] = "VALORES_PRESENTES" })
            .Should().BeEquivalentTo(new { Fato = "HA_MEMBRO", Eixo = "operação do agregado" });
        publicadas.PrimeiroEixoCongeladoTrocado(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["NAO_COLETADO"] = new("CATEGORICO", "ESCALAR", "INSCRICAO") }, operacoes)
            .Should().BeNull("o fato que nenhuma versão coletou não é afetado");
    }

    private static ProcessoSeletivo Publicado()
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma habilitacao = Fase(2, FormularioProcesso.CodigoFaseHabilitacao);
        FaseCronograma matricula = Fase(3, "MATRICULA");
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        processo.DefinirCronogramaFases([inscricao, habilitacao, matricula], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Fato("TEM_RENDA", 0)], grupos: [Familia(1, Campo("MEMBRO_RENDA", 0))]).IsSuccess.Should().BeTrue();

        GrafoConfiguracao publicada = VersoesPublicadasDeTeste.DoProcesso(processo);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        VersaoConfiguracao versaoBase = VersaoConfiguracao.Abrir(
            processo.Id, Encoding.UTF8.GetBytes("{}"), "1.1", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", Agora);
        processo.AbrirRetificacao("Correção da coleta", versaoBase, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SemEixosCongelados([publicada]), "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FatoColetado Fato(string codigo, int ordem, TipoRenderizacao tipo = TipoRenderizacao.Booleano, string classificacao = "PESSOAL") =>
        FatoColetado.Criar(codigo, ordem, codigo, tipo, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: classificacao).Value!;

    /// <summary>O campo de grupo não declara seção: ela é a do grupo.</summary>
    private static FatoColetado Campo(string codigo, int ordem, TipoRenderizacao tipo = TipoRenderizacao.Booleano, string classificacao = "PESSOAL") =>
        FatoColetado.Criar(codigo, ordem, codigo, tipo, Obrigatoriedade.Sempre, null, classificacaoProtecao: classificacao).Value!;

    private static GrupoColetado Familia(int ordem, params FatoColetado[] campos) =>
        GrupoColetado.Criar("FAMILIA", ordem, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre, [.. campos]).Value!;
}

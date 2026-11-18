namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Na retificação, o fato que uma versão publicada já coletou não troca de eixo (UNI-REQ-0144): o
/// candidato pode tê-lo respondido em qualquer versão, e a classificação e a elegibilidade leem as
/// respostas juntas. A comparação é com o que a versão publicada congelou, nunca com o catálogo.
/// </summary>
public sealed class RetificacaoEixoDoFatoColetadoTests
{
    private static readonly EixosDoMetadadoCongelado Booleano = new("BOOLEANO", "ESCALAR", "INSCRICAO");
    private static readonly EixosDoMetadadoCongelado Categorico = new("CATEGORICO", "ESCALAR", "INSCRICAO");

    [Fact(DisplayName = "O metadado e a operação congelados só recusam quando a versão nova os congela diferentes")]
    public void MetadadoEOperacao_ComparaComOQueAVersaoNovaCongela()
    {
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.De(
            [VersoesPublicadasDeTeste.Versao([], [Fato("TEM_RENDA", 0)])],
            [new EixosDeVersaoPublicada(
                new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = Booleano },
                new Dictionary<string, string> { ["HA_MEMBRO"] = "EXISTE" })]);
        Dictionary<string, string> operacoes = new() { ["HA_MEMBRO"] = "EXISTE" };

        publicadas.EixosCongeladosTrocados(new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = Booleano }, operacoes)
            .Should().BeEmpty("o que a versão nova congela é o que a versão publicada congelou");
        publicadas.EixosCongeladosTrocados(new Dictionary<string, EixosDoMetadadoCongelado>(), operacoes)
            .Should().BeEmpty("a versão nova que não cita o fato numa condição não o congela: uma edição do catálogo não conta");
        publicadas.EixosCongeladosTrocados(new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = Categorico }, operacoes)
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Fato = "TEM_RENDA", Eixo = "domínio" });
        publicadas.EixosCongeladosTrocados(
            new Dictionary<string, EixosDoMetadadoCongelado>(), new Dictionary<string, string> { ["HA_MEMBRO"] = "VALORES_PRESENTES" })
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { Fato = "HA_MEMBRO", Eixo = "operação do agregado" });
        publicadas.EixosCongeladosTrocados(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["NAO_COLETADO"] = Categorico }, operacoes)
            .Should().BeEmpty("o fato que nenhuma versão coletou não é afetado");
    }

    [Fact(DisplayName = "Todas as trocas de eixo saem juntas: os metadados primeiro, depois as operações dos agregados, cada fato uma vez, na ordem de entrada")]
    public void EixosCongeladosTrocados_AcumulamMetadadosEDepoisAgregados()
    {
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.De(
            [VersoesPublicadasDeTeste.Versao([], [Fato("TEM_RENDA", 0), Fato("TEM_FILHOS", 1)])],
            [new EixosDeVersaoPublicada(
                new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_RENDA"] = Booleano, ["TEM_FILHOS"] = Booleano },
                new Dictionary<string, string> { ["HA_MEMBRO"] = "EXISTE", ["OUTRO_MEMBRO"] = "EXISTE" })]);

        IReadOnlyList<TrocaDeEixo> trocas = publicadas.EixosCongeladosTrocados(
            new Dictionary<string, EixosDoMetadadoCongelado> { ["TEM_FILHOS"] = Categorico, ["TEM_RENDA"] = Categorico },
            new Dictionary<string, string> { ["OUTRO_MEMBRO"] = "VALORES_PRESENTES", ["HA_MEMBRO"] = "VALORES_PRESENTES" });

        trocas.Select(static troca => troca.Fato).Should().Equal("TEM_FILHOS", "TEM_RENDA", "OUTRO_MEMBRO", "HA_MEMBRO");
        trocas.Select(static troca => troca.DeAgregado).Should().Equal(false, false, true, true);
    }

    private static FatoColetado Fato(string codigo, int ordem) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}

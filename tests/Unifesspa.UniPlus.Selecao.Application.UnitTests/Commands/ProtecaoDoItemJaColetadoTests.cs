namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O item de um fato que a versão publicada já coletou mantém a hipótese legal e a finalidade que ela congelou,
/// e adota a descrição e o componente de exibição de hoje; corrigir o catálogo depois não troca o eixo na retificação.
/// </summary>
public sealed class ProtecaoDoItemJaColetadoTests
{
    private static ContextoDoCatalogo Contexto(FatoCandidatoView catalogo, FatosDasVersoesPublicadas? publicadas)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Proteção do item", out _);
        return ContextoDoCatalogo.De(processo, [catalogo]) with { Publicadas = publicadas };
    }

    private static FatosDasVersoesPublicadas Publicadas(string fato, string hipotese, string finalidade) =>
        FatosDasVersoesPublicadas.Reconstituir(
            new Dictionary<FinalidadeFormulario, IReadOnlyCollection<string>>(),
            [],
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new Dictionary<string, IReadOnlyCollection<EixosDoItemCongelado>>
            {
                [fato] = [new EixosDoItemCongelado("TEXTO", "CATALOGO", "LIVRE", "PESSOAL", hipotese, finalidade)],
            });

    private static FatoCandidatoView Catalogo(string finalidade) =>
        CatalogoDoConjuntoBasico.Fatos.Single(f => f.Codigo == "NOME_MAE") with
        {
            HipoteseLegal = "EXECUCAO_POLITICAS_PUBLICAS",
            FinalidadeTratamento = finalidade,
            Descricao = "Descrição corrigida no catálogo.",
            ComponenteExibicao = "MOEDA",
        };

    [Fact(DisplayName = "O fato já coletado mantém a finalidade e a hipótese que a versão publicada congelou, e adota a descrição e o componente do catálogo corrigido")]
    public void FatoJaColetado_UsaOCongeladoNaVersao()
    {
        FatoCandidatoView catalogo = Catalogo("Identificação corrigida no catálogo.");
        ContextoDoCatalogo contexto = Contexto(catalogo, Publicadas("NOME_MAE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identificação do candidato."));

        contexto.TextosDoItem(catalogo).Should().Be(new TextosDoItem(
            "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identificação do candidato.", "Descrição corrigida no catálogo.", "MOEDA"));
    }

    [Fact(DisplayName = "Versões publicadas que congelaram os mesmos eixos: o item regravado os copia")]
    public void VersoesSemDivergencia_CopiaOCongelado()
    {
        EixosDoItemCongelado eixos = new("TEXTO", "CATALOGO", "LIVRE", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade congelada.");
        FatoCandidatoView catalogo = Catalogo("Finalidade do catálogo.");
        ContextoDoCatalogo contexto = Contexto(catalogo, FatosDasVersoesPublicadas.Reconstituir(
            new Dictionary<FinalidadeFormulario, IReadOnlyCollection<string>>(),
            [],
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new Dictionary<string, IReadOnlyCollection<EixosDoItemCongelado>> { ["NOME_MAE"] = [eixos, eixos] }));

        contexto.TextosDoItem(catalogo).Should().Be(new TextosDoItem(
            "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade congelada.", "Descrição corrigida no catálogo.", "MOEDA"));
    }

    [Fact(DisplayName = "Versões publicadas que congelaram eixos diferentes: a retificação recusa o item, qualquer que seja a cópia")]
    public void VersoesDivergentes_RetificacaoRecusa()
    {
        EixosDoItemCongelado antiga = new("TEXTO", "CATALOGO", "LIVRE", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade antiga.");
        EixosDoItemCongelado recente = antiga with { HipoteseLegal = "EXECUCAO_POLITICAS_PUBLICAS", FinalidadeTratamento = "Finalidade recente." };
        FatosDasVersoesPublicadas publicadas = FatosDasVersoesPublicadas.Reconstituir(
            new Dictionary<FinalidadeFormulario, IReadOnlyCollection<string>>(),
            [],
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new Dictionary<string, IReadOnlyCollection<EixosDoItemCongelado>> { ["NOME_MAE"] = [antiga, recente] });
        FatoColetado item = FatoColetado.Criar(
            "NOME_MAE", 0, "Nome da mãe", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, "PESSOAL",
            antiga.HipoteseLegal, antiga.FinalidadeTratamento, ProtecaoDoFatoDeTeste.Descricao, formato: "LIVRE").Value!;

        publicadas.PrimeiroItemComEixoTrocado([item], []).Should().NotBeNull();
    }

    [Fact(DisplayName = "O fato novo na retificação traz a hipótese, a finalidade, a descrição e o componente do catálogo de hoje")]
    public void FatoNovo_VemDoCatalogo()
    {
        FatoCandidatoView catalogo = Catalogo("Finalidade do catálogo.");
        ContextoDoCatalogo contexto = Contexto(catalogo, Publicadas("OUTRO_FATO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Outra."));

        contexto.TextosDoItem(catalogo).Should().Be(new TextosDoItem(
            "EXECUCAO_POLITICAS_PUBLICAS", "Finalidade do catálogo.", "Descrição corrigida no catálogo.", "MOEDA"));
    }

    [Fact(DisplayName = "A finalidade recopiada do catálogo fica sem espaço nas pontas e em NFC, como o envelope a congela")]
    public void Recopia_NormalizaAFinalidade()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Proteção do item", out _);
        FatoColetado item = FatoColetado.Criar(
            "CPF", 0, "CPF", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, "PESSOAL",
            ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoDoFatoDeTeste.Descricao, formato: "CPF").Value!;
        processo.DefinirItens([item]).IsSuccess.Should().BeTrue();
        IReadOnlyList<FatoCandidatoView> catalogo = [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "CPF"
            ? f with { HipoteseLegal = " EXECUCAO_POLITICAS_PUBLICAS ", FinalidadeTratamento = "  Identifica\u00e7\u00e3o: a\u0301.  " }
            : f)];

        ReconferenciaDeTeste.Aplicar(processo, catalogo, null).IsSuccess.Should().BeTrue();

        FatoColetado recopiado = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        recopiado.HipoteseLegal.Should().Be("EXECUCAO_POLITICAS_PUBLICAS");
        recopiado.FinalidadeTratamento.Should().Be("Identifica\u00e7\u00e3o: \u00e1.");
    }

    [Fact(DisplayName = "Retificar depois de corrigir a descrição no catálogo leva a descrição nova ao fato já coletado, sem mudar a proteção")]
    public void Retificacao_AdotaADescricaoCorrigidaDoFatoJaColetado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Proteção do item", out _);
        FatoColetado item = FatoColetado.Criar(
            "CPF", 0, "CPF", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, "PESSOAL",
            ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoDoFatoDeTeste.Descricao, formato: "CPF").Value!;
        processo.DefinirItens([item]).IsSuccess.Should().BeTrue();
        IReadOnlyList<FatoCandidatoView> catalogo = [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "CPF"
            ? f with { Descricao = "Descrição corrigida no catálogo.", ComponenteExibicao = "MOEDA", FinalidadeTratamento = "Outra finalidade." }
            : f)];

        ReconferenciaDeTeste.Aplicar(processo, catalogo, new HashSet<string>(["CPF"], StringComparer.Ordinal)).IsSuccess.Should().BeTrue();

        FatoColetado retificado = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        (retificado.Descricao, retificado.ComponenteExibicao).Should().Be(("Descrição corrigida no catálogo.", "MOEDA"));
        retificado.FinalidadeTratamento.Should().Be(ProtecaoDoFatoDeTeste.Finalidade);
    }
}

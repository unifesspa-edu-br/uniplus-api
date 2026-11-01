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
/// O item de um fato que a versão publicada já coletou mantém a hipótese legal e a finalidade que ela
/// congelou; corrigir o catálogo depois não troca o eixo na retificação.
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
        };

    [Fact(DisplayName = "O fato já coletado mantém a finalidade e a hipótese que a versão publicada congelou, e não as do catálogo corrigido")]
    public void FatoJaColetado_UsaOCongeladoNaVersao()
    {
        FatoCandidatoView catalogo = Catalogo("Identificação corrigida no catálogo.");
        ContextoDoCatalogo contexto = Contexto(catalogo, Publicadas("NOME_MAE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identificação do candidato."));

        contexto.ProtecaoDoItem(catalogo).Should().Be(("CUMPRIMENTO_OBRIGACAO_LEGAL", "Identificação do candidato."));
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

        contexto.ProtecaoDoItem(catalogo).Should().Be(("CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade congelada."));
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
            antiga.HipoteseLegal, antiga.FinalidadeTratamento, formato: "LIVRE").Value!;

        publicadas.PrimeiroItemComEixoTrocado([item], []).Should().NotBeNull();
    }

    [Fact(DisplayName = "O fato novo na retificação traz a hipótese e a finalidade do catálogo de hoje")]
    public void FatoNovo_VemDoCatalogo()
    {
        FatoCandidatoView catalogo = Catalogo("Finalidade do catálogo.");
        ContextoDoCatalogo contexto = Contexto(catalogo, Publicadas("OUTRO_FATO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Outra."));

        contexto.ProtecaoDoItem(catalogo).Should().Be(("EXECUCAO_POLITICAS_PUBLICAS", "Finalidade do catálogo."));
    }

    [Fact(DisplayName = "A finalidade recopiada do catálogo fica sem espaço nas pontas e em NFC, como o envelope a congela")]
    public void Recopia_NormalizaAFinalidade()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Proteção do item", out _);
        FatoColetado item = FatoColetado.Criar(
            "CPF", 0, "CPF", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, "PESSOAL",
            ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, formato: "CPF").Value!;
        processo.DefinirItens([item]).IsSuccess.Should().BeTrue();
        IReadOnlyList<FatoCandidatoView> catalogo = [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "CPF"
            ? f with { HipoteseLegal = " EXECUCAO_POLITICAS_PUBLICAS ", FinalidadeTratamento = "  Identifica\u00e7\u00e3o: a\u0301.  " }
            : f)];

        ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null).IsSuccess.Should().BeTrue();

        FatoColetado recopiado = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        recopiado.HipoteseLegal.Should().Be("EXECUCAO_POLITICAS_PUBLICAS");
        recopiado.FinalidadeTratamento.Should().Be("Identifica\u00e7\u00e3o: \u00e1.");
    }
}

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
/// A recópia do item a partir do catálogo: a finalidade fica sem espaço nas pontas e em NFC, e o fato que a versão
/// publicada já coletou adota a descrição e o componente de exibição de hoje sem trocar a proteção que ela congelou.
/// </summary>
public sealed class ProtecaoDoItemJaColetadoTests
{
    [Fact(DisplayName = "A finalidade recopiada do catálogo fica sem espaço nas pontas e em NFC, como o envelope a congela")]
    public void Recopia_NormalizaAFinalidade()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Proteção do item", out _);
        FatoColetado item = FatoColetadoDeTeste.Coletando("CPF", 0).DeTexto("CPF").Construir();
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
        FatoColetado item = FatoColetadoDeTeste.Coletando("CPF", 0).DeTexto("CPF").Construir();
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

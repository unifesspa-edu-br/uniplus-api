namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Desativar um fato ou um valor no catálogo recusa só vínculo novo (ADR-0136): a configuração que
/// já usava o fato ou citava o valor continua com ele, e a publicação congela os valores ativos
/// mais os que as condições do processo já citam.
/// </summary>
public sealed class DesativacaoNoCatalogoTests
{
    private static readonly FatoValorDominioViewItem Branca = new("BRANCA", "Branca.", 0, true);
    private static readonly FatoValorDominioViewItem PretaDesativada = new("PRETA", "Preta.", 1, false);
    private static readonly FatoValorDominioViewItem Parda = new("PARDA", "Parda.", 2, true);

    private static FatoCandidatoView CorRaca(bool ativo = true) => new(
        Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
        ["BRANCA", "PRETA", "PARDA"], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", [Branca, PretaDesativada, Parda], "GLOBAL", ativo, "PESSOAL", ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static Dictionary<string, FatoCandidatoView> Catalogo(FatoCandidatoView fato) =>
        new(StringComparer.Ordinal) { [fato.Codigo] = fato };

    private static VinculosDeFatos Citando(string fato, string valor) =>
        VinculosDeFatos.De([], [(fato, JsonSerializer.SerializeToElement(valor))]);

    private static VinculosDeFatos Nenhum => VinculosDeFatos.De([], []);

    [Fact(DisplayName = "Fato desativado: vínculo novo é recusado, o existente continua")]
    public void FatoDesativado_RecusaSoVinculoNovo()
    {
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca(ativo: false));
        VinculosDeFatos coleta = VinculosDeFatos.De(["COR_RACA"], []);

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, coleta).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        ConferenciaDeVinculoNovo.Conferir(catalogo, coleta, coleta).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Fato sem a proteção aprovada: vínculo novo é recusado, o existente continua")]
    public void FatoComProtecaoPendente_RecusaSoVinculoNovo()
    {
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca() with { ProtecaoAprovada = false });
        VinculosDeFatos coleta = VinculosDeFatos.De(["COR_RACA"], []);

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, coleta).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoComProtecaoPendente);
        ConferenciaDeVinculoNovo.Conferir(catalogo, coleta, coleta).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Valor desativado: condição nova que o cita é recusada; a que já o citava continua")]
    public void ValorDesativado_RecusaSoCitacaoNova()
    {
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca());

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, Citando("COR_RACA", "PRETA"))
            .Error!.Code.Should().Be(VinculoCatalogoErrorCodes.ValorDesativado);
        ConferenciaDeVinculoNovo.Conferir(catalogo, Citando("COR_RACA", "PRETA"), Citando("COR_RACA", "PRETA"))
            .IsSuccess.Should().BeTrue();
        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, Citando("COR_RACA", "PARDA")).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Regra de derivação nova que contribui valor desativado é recusada; a que já o contribuía continua")]
    public void ValorDesativadoContribuido_RecusaSoRegraNova()
    {
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca());
        VinculosDeFatos contribuiPreta = VinculosDeFatos.De([], [], [("COR_RACA", "PRETA")]);

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, contribuiPreta)
            .Error!.Code.Should().Be(VinculoCatalogoErrorCodes.ValorDesativado);
        ConferenciaDeVinculoNovo.Conferir(catalogo, contribuiPreta, contribuiPreta).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A publicação congela os valores ativos e o desativado que uma condição do processo já cita")]
    public void Congelamento_ValoresAtivosMaisOsCitados()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS Desativação", OrigemCandidatos.InscricaoPropria);
        processo.DefinirItens([CorRacaColetada()], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca());

        ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["COR_RACA"]!
            .Select(static v => v.Codigo).Should().Equal("BRANCA", "PARDA");

        processo.DefinirItens([CorRacaColetada(), BaixaRendaExibidaQuandoPreta()], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        catalogo["BAIXA_RENDA"] = BaixaRendaNoCatalogo();

        ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["COR_RACA"]!
            .Select(static v => v.Codigo).Should().Equal("BRANCA", "PRETA", "PARDA");
    }

    private static FatoColetado CorRacaColetada() => FatoColetadoDeTeste.Coletando("COR_RACA", 0)
        .ComRotulo("Cor ou raça")
        .DoTipo(TipoRenderizacao.SelecaoUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .Construir();

    private static FatoColetado BaixaRendaExibidaQuandoPreta() => FatoColetadoDeTeste.Coletando("BAIXA_RENDA", 1)
        .ComRotulo("Baixa renda")
        .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
        .Construir();

    private static FatoCandidatoView BaixaRendaNoCatalogo() => new(
        Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR",
        null, "INSCRICAO", "CAMPO_FORMULARIO:BAIXA_RENDA", null, null, true, "PESSOAL", ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static DocumentoExigido ExigenciaCondicionadaACorOuRaca(Guid faseId) =>
        DocumentoExigidoDeTeste.Novo()
            .NaFase(faseId)
            .ComTipo("AUTODECLARACAO", "Autodeclaração", "PESSOAL")
            .Condicional([CondicaoGatilho.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("BRANCA")).Value!])
            .ComBasesLegais([DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!])
            .NaFinalidade(FinalidadeFormulario.Inscricao)
            .Criar().Value!;

    [Fact(DisplayName = "O metadado do fato citado em gatilho congela o mesmo vocabulário: sem o valor desativado que nada cita")]
    public void Metadado_SemValorDesativadoNaoCitado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Metadado", out Guid faseId);
        DocumentoExigido exigencia = ExigenciaCondicionadaACorOuRaca(faseId);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!])
            .IsSuccess.Should().BeTrue();

        MetadadoFatoCongelado metadado = ResolvedorMetadadosFatosCongelados.Resolver(processo, Catalogo(CorRaca())).Value!["COR_RACA"];

        metadado.ValoresDominio.Should().Equal("BRANCA", "PARDA");
        metadado.ValoresDominioDeclarados!.Select(static v => v.Codigo).Should().Equal("BRANCA", "PARDA");
    }
}

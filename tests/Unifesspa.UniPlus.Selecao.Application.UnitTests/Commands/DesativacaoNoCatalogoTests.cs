namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
        Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
        ["BRANCA", "PRETA", "PARDA"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", [Branca, PretaDesativada, Parda], "GLOBAL", ativo);

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

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, coleta).Error!.Code.Should().Be("ProcessoSeletivo.FatoDesativado");
        ConferenciaDeVinculoNovo.Conferir(catalogo, coleta, coleta).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Valor desativado: condição nova que o cita é recusada; a que já o citava continua")]
    public void ValorDesativado_RecusaSoCitacaoNova()
    {
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca());

        ConferenciaDeVinculoNovo.Conferir(catalogo, Nenhum, Citando("COR_RACA", "PRETA"))
            .Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioDesativado");
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
            .Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioDesativado");
        ConferenciaDeVinculoNovo.Conferir(catalogo, contribuiPreta, contribuiPreta).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A publicação congela os valores ativos e o desativado que uma condição do processo já cita")]
    public void Congelamento_ValoresAtivosMaisOsCitados()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Desativação", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirItens(
            [FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = Catalogo(CorRaca());

        ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["COR_RACA"]!
            .Select(static v => v.Codigo).Should().Equal("BRANCA", "PARDA");

        processo.DefinirItens(
            [FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null).Value!,
             FatoColetado.Criar("BAIXA_RENDA", 1, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
                 [CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        catalogo["BAIXA_RENDA"] = new FatoCandidatoView(
            Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", null, "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:BAIXA_RENDA", null, null, true);

        ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["COR_RACA"]!
            .Select(static v => v.Codigo).Should().Equal("BRANCA", "PRETA", "PARDA");
    }

    [Fact(DisplayName = "O metadado do fato citado em gatilho congela o mesmo vocabulário: sem o valor desativado que nada cita")]
    public void Metadado_SemValorDesativadoNaoCitado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Metadado", out Guid faseId);
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "AUTODECLARACAO", "Autodeclaração", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("BRANCA")).Value!],
            basesLegais: [DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        MetadadoFatoCongelado metadado = ResolvedorMetadadosFatosCongelados.Resolver(processo, Catalogo(CorRaca())).Value!["COR_RACA"];

        metadado.ValoresDominio.Should().Equal("BRANCA", "PARDA");
        metadado.ValoresDominioDeclarados!.Select(static v => v.Codigo).Should().Equal("BRANCA", "PARDA");
    }
}

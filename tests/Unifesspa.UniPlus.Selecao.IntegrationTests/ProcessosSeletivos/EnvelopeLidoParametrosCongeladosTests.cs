namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class EnvelopeLidoParametrosCongeladosTests
{
    [Fact]
    public void Reidratar_MetadadoCongelado_ConservaOBindingSemCatalogoVivo()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        MetadadoFatoCongelado metadado = new("COR_RACA", "CATEGORICO", "DECLARADO", "ESCALAR", "INSCRICAO",
            "CAMPO_FORMULARIO:COR_RACA", ["VALOR_CONGELADO"], null);
        EntradaCanonicalizacao entrada = EntradaComMetadado(processo, metadado);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.DadosDeCatalogo.MetadadosFatosCongelados!["COR_RACA"].Should().BeEquivalentTo(metadado);
    }

    [Fact]
    public void Reidratar_FusoDiferenteDoInstitucional_RecodificaPeloCongelado()
    {
        const string FusoCongelado = "America/Manaus";
        FusoCongelado.Should().NotBe(FusoInstitucional.ZoneId);
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo) with { FusoHorario = FusoCongelado });
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.FusoHorario.Should().Be(FusoCongelado);
    }

    [Fact]
    public void Reidratar_ValoresEstaticosEDoProcesso_ConservaConteudoEOrdem()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        DefinirCondicaoDeAtendimento(processo);
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(ExemploDeEnvelopeDadosDoEdital.ValoresSelecionaveisRicos())
        {
            ["CONDICAO_ATENDIMENTO"] = [new("LACTANTE", "Lactante", 0), new(AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência", 1)],
        };
        EntradaCanonicalizacao entrada = EntradaComValores(processo, valores);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        ConferirValores(lido.DadosDeCatalogo.ValoresSelecionaveisCongelados!);
    }

    private static void DefinirCondicaoDeAtendimento(ProcessoSeletivo processo) =>
        processo.DefinirItens([
            Campo("COR_RACA", 0, TipoRenderizacao.SelecaoUnica),
            Campo("RENDA", 1, TipoRenderizacao.SelecaoUnica),
            Campo("CONDICAO_ATENDIMENTO", 2, TipoRenderizacao.SelecaoMultipla),
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            Campo(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 4, TipoRenderizacao.Booleano),
        ], grupos: []).IsSuccess.Should().BeTrue();

    private static FatoColetado Campo(string codigo, int ordem, TipoRenderizacao tipo) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).ComRotulo("Campo de teste").DoTipo(tipo).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();

    private static void ConferirValores(IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores)
    {
        valores["COR_RACA"]!.Select(v => v.Codigo).Should().Equal("BRANCA", "PRETA", "PARDA");
        valores["COR_RACA"]!.Select(v => v.Descricao).Should().Equal(
            "Autodeclaração de cor/raça branca.", "Preta", "Autodeclaração de cor/raça parda.");
        valores["COR_RACA"]!.Select(v => v.Orientacao).Should().Equal(
            null, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação.", null);
        valores["CONDICAO_ATENDIMENTO"]!.Select(v => (v.Codigo, v.Descricao, v.Ordem)).Should().Equal(
            ("LACTANTE", "Lactante", 0), (AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência", 1));
        valores.Should().ContainKey("RENDA");
    }
    private static EntradaCanonicalizacao EntradaComMetadado(ProcessoSeletivo processo, MetadadoFatoCongelado metadado) =>
        ExemploDeEnvelopeEntrada.Entrada(processo) with
        {
            DadosDeCatalogo = ExemploDeEnvelopeEntrada.Entrada(processo).DadosDeCatalogo with
            {
                MetadadosFatosCongelados = new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal) { ["COR_RACA"] = metadado },
            },
        };

    private static EntradaCanonicalizacao EntradaComValores(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) =>
        ExemploDeEnvelopeEntrada.Entrada(processo) with
        {
            DadosDeCatalogo = ExemploDeEnvelopeEntrada.Entrada(processo).DadosDeCatalogo with
            {
                ValoresSelecionaveisCongelados = CatalogoDoConjuntoBasico.ComValoresCongelados(valores),
            },
        };

}

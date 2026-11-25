namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class EnvelopeLidoPredicadosTests
{
    [Fact]
    public void Reidratar_OrdinaisNaoContiguos_ConservaAConjuncaoEReproduzOsBytes()
    {
        ProcessoSeletivo processo = PredicadoDnfOrdenacaoCanonicaTests.MontarProcessoComGatilho([
            Condicao(7, "FATO_A", "valorA"), Condicao(7, "FATO_B", "valorB"),
            Condicao(4, "FATO_C", "valorC"), Condicao(9, "FATO_D", "valorD"),
        ]);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        IReadOnlyCollection<CondicaoGatilho> condicoes = lido.Configuracao.Exigencias.DocumentosExigidos.Single().Condicoes;
        condicoes.Select(c => c.Clausula).Distinct().Should().BeEquivalentTo([0, 1, 2]);
        condicoes.GroupBy(c => c.Clausula).Should().ContainSingle(g => g.Count() == 2)
            .Which.Select(c => c.Fato).Should().BeEquivalentTo("FATO_A", "FATO_B");
    }

    [Fact]
    public void Reidratar_PredicadoCustomizadoDecimal_ConservaONumeroEReproduzOsBytes()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        RegraAvaliada regra = RegraCustomizada();
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo,
            conformidade: new ResultadoConformidade([regra], [])));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Customizado predicado = lido.Conformidade!.Regras.Single().Predicado.Should().BeOfType<Customizado>().Subject;
        predicado.Parametros.GetProperty("limite").ValueKind.Should().Be(JsonValueKind.Number);
        predicado.Parametros.GetProperty("limite").GetDecimal().Should().Be(1.5m);
        predicado.Parametros.GetProperty("regra").GetString().Should().Be("transitoria");
        lido.Conformidade.Regras.Single().Should().BeEquivalentTo(regra);
    }

    [Fact]
    public void Reidratar_FaixaEtariaNoFimDaFase_ConservaAReferenciaEReproduzOsBytes()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ApoioDoEnvelopeCodec.ProcessoDeInscricao("PS faixa etária");
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(DeclaracaoDeMaioridade(fase), 0).Value!]).IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!)
            .IsSuccess.Should().BeTrue();
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo,
            ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.Configuracao.Cronograma.CronogramaFases.Single().Id.Should().Be(fase.Id);
        lido.Configuracao.Coleta.ReferenciaTemporalFatos!.Tipo.Should().Be(ReferenciaTipo.FimFase);
        lido.Configuracao.Coleta.ReferenciaTemporalFatos.FaseId.Should().Be(fase.Id);
        lido.Configuracao.Exigencias.DocumentosExigidos.Single().Condicoes.Should().ContainSingle().Which.Fato.Should().Be("FAIXA_ETARIA");
    }

    private static CondicaoGatilho Condicao(int clausula, string fato, string valor) =>
        CondicaoGatilho.Criar(clausula, fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    private static RegraAvaliada RegraCustomizada() => new(
        RegraId: Guid.CreateVersion7(), RegraCodigo: "REGRA_CUSTOMIZADA", Categoria: CategoriaObrigatoriedade.Outros,
        TipoProcessoCodigoAvaliado: "SiSU", Predicado: new Customizado(JsonSerializer.SerializeToElement(new { limite = 1.5m, regra = "transitoria" })),
        Aprovada: true, Motivo: null, BaseLegal: "Lei de teste", AtoNormativoUrl: null, PortariaInterna: null,
        DescricaoHumana: "Regra com parâmetro decimal", VigenciaInicio: new DateOnly(2020, 1, 1), VigenciaFim: null, Hash: new string('c', 64));

    private static DocumentoExigido DeclaracaoDeMaioridade(FaseCronograma fase) => DocumentoExigido.Criar(
        Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_declaracao_maioridade").Value!,
        new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(fase.Id, null, Unifesspa.UniPlus.Regras.Formularios.FinalidadeFormulario.Inscricao),
            new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!]),
            new IndeferimentoDoDocumento(true, null, [DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!]),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null)).Value!;
}

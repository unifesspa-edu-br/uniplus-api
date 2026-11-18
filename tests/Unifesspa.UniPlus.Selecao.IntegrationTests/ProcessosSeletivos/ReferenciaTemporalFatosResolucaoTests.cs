namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// <c>dataReferenciaFatos</c> — a <see cref="DateOnly"/> resolvida e congelada no
/// envelope 1.2 a partir da política <see cref="ReferenciaTemporalFatos"/> (Story #554,
/// PR #903, B-03). Complementa <c>ProcessoSeletivoPublicarTests</c> (que prova os bloqueios
/// de publicação quando a resolução falha) com o caso em que ela FUNCIONA: cada
/// congelamento resolve a política vigente NO MOMENTO em que congela, e uma retificação
/// que muda a política depois não reabre nem altera o que já foi congelado.
/// </summary>
/// <remarks>A projeção é pura (ADR-0109 D6) — esta suíte não precisa de banco.</remarks>
public sealed class ReferenciaTemporalFatosResolucaoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly string HashFixo = new('a', 64);

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static ProcessoSeletivo NovoProcessoConformeComGatilhoPorFaixaEtaria(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme("PS ReferenciaTemporalFatos 2026");

        FaseCronograma fase = processo.CronogramaFases.Single();
        faseId = fase.Id;

        DocumentoExigido exigencia = DeclaracaoDeMaioridade(fase.Id);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    private static string? DataReferenciaFatos(SnapshotCanonico snapshot)
    {
        JsonObject envelope = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();
        return envelope["documentosExigidos"]!["dataReferenciaFatos"]?.GetValue<string>();
    }

    [Fact(DisplayName = "B-03: uma retificação que muda a política de ReferenciaTemporalFatos não altera a dataReferenciaFatos já congelada de uma versão anterior")]
    public void RetificacaoMudaPolitica_PreservaDataReferenciaFatosCongeladaDaVersaoAnterior()
    {
        ProcessoSeletivo processo = NovoProcessoConformeComGatilhoPorFaixaEtaria(out _);
        SnapshotCanonico congeladoV1 = CongelarNaData(processo, new DateOnly(2026, 1, 15));
        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15");

        SnapshotCanonico congeladoV2 = CongelarNaData(processo, new DateOnly(2026, 2, 20));
        DataReferenciaFatos(congeladoV2).Should().Be("2026-02-20",
            "pré-condição: o congelamento SEGUINTE resolve a política vigente no momento em que congela");

        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15",
            "B-03: cada VersaoConfiguracao congela sua PRÓPRIA política — o envelope V1, já congelado, é " +
            "imutável (ADR-0104); a retificação que mudou a política para produzir V2 não o reabre nem o altera");
    }

    private static DocumentoExigido DeclaracaoDeMaioridade(Guid faseId)
    {
        CondicaoGatilho condicao = GatilhoDeMaioridade();
        DocumentoExigidoBaseLegal baseLegal = BaseLegalDaMaioridade();
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        return exigencia;
    }


    private static CondicaoGatilho GatilhoDeMaioridade()
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        return condicao;
    }


    private static DocumentoExigidoBaseLegal BaseLegalDaMaioridade()
    {
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        return baseLegal;
    }


    private static SnapshotCanonico CongelarNaData(ProcessoSeletivo processo, DateOnly data)
    {
        processo.DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, data, null).Value!)
            .IsSuccess.Should().BeTrue();
        return Canonicalizar(processo);
    }

}

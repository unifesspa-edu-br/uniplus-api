namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoProcessoSeletivoEmRascunho;

/// <summary>
/// Distribuição de vagas por oferta de curso do <see cref="ProcessoSeletivo"/> (Story #773):
/// o vínculo com a raiz, as recusas de coerência entre ofertas e a concorrência dupla derivada.
/// </summary>
public sealed class ProcessoSeletivoDistribuicaoDeVagasTests
{
    [Fact(DisplayName = "DefinirDistribuicaoVagas vincula a configuração à raiz (Story #773)")]
    public void DefinirDistribuicaoVagas_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas configuracao = Oferta(Guid.CreateVersion7(), 50, [Ac(50)]);

        Result result = processo.DefinirDistribuicaoVagas([configuracao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue();
        processo.DistribuicaoVagas.Should().ContainSingle();
        processo.DistribuicaoVagas.Single().ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com AcaoQuandoIndeferido divergente para o mesmo código entre ofertas é recusada (CA-16)")]
    public void DefinirDistribuicaoVagas_AcaoQuandoIndeferidoDivergente_Falha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaA = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV("RECLASSIFICAR_AC", 2), Ac(8)]);
        ConfiguracaoDistribuicaoVagas ofertaB = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV("RECLASSIFICAR_REGRA_EDITAL", 2), Ac(8)]);

        Result result = processo.DefinirDistribuicaoVagas([ofertaA, ofertaB], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com a mesma modalidade copiada com regras diferentes entre ofertas é recusada")]
    public void DefinirDistribuicaoVagas_RegraDeAplicabilidadeDivergente_Falha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaA = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV(null, 2, Exige("PCD")), Ac(8)]);
        ConfiguracaoDistribuicaoVagas ofertaB = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV(null, 2, Exige("CONCORRER_PCD")), Ac(8)]);

        Result result = processo.DefinirDistribuicaoVagas([ofertaA, ofertaB], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue("uma modalidade tem uma regra só no processo");
        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente);
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas vazia é recusada")]
    public void DefinirDistribuicaoVagas_Vazia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirDistribuicaoVagas([], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DistribuicaoVagasVazia");
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com oferta de curso duplicada é recusada")]
    public void DefinirDistribuicaoVagas_OfertaCursoDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ofertaCursoId = Guid.CreateVersion7();

        Result result = processo.DefinirDistribuicaoVagas(
        [
            Oferta(ofertaCursoId, 50, [Ac(50)]),
            Oferta(ofertaCursoId, 50, [Ac(50)]),
        ], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OfertaCursoDuplicada");
    }

    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é falsa sem distribuição de vagas com cota reservada")]
    public void ConcorrenciaDuplaAplicavel_SemCotaReservada_Falsa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas([Oferta(Guid.CreateVersion7(), 50, [Ac(50)])], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(EstadoDaConfiguracao.De(processo)).Should().BeFalse();
    }

    // Só exercita o cenário mínimo de derivação: a regra institucional evita que a matriz das
    // 8 federais + AC (INV-6), validada por ConfiguracaoDistribuicaoVagas.Criar, entre no teste.
    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é derivada (true) quando há modalidade de cota reservada (INV-B7)")]
    public void ConcorrenciaDuplaAplicavel_ComCotaReservada_Verdadeira()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas distribuicao = Oferta(Guid.CreateVersion7(), 50, [Ac(30), CotaReservadaPpi(20)]);

        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(EstadoDaConfiguracao.De(processo)).Should().BeTrue();
    }

    private static ConfiguracaoDistribuicaoVagas Oferta(Guid ofertaCursoId, int voBase, IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, voBase, pr: 1m,
            ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null, referenciaDemografica: null, modalidades, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static ModalidadeSelecionada Ac(int quantidade) => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
        null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: quantidade).Value!;

    private static ModalidadeSelecionada ModalidadeV(string? acaoQuandoIndeferido, int quantidade, PredicadoDnf? regraDeAplicabilidade = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
            regraDeAplicabilidade ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa),
            acaoQuandoIndeferido, "base legal", quantidadeDeclarada: quantidade).Value!;

    private static ModalidadeSelecionada CotaReservadaPpi(int quantidade) => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), "LB_PPI", null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
        null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), null, "Lei 12.711/2012 art. 3º", quantidadeDeclarada: quantidade).Value!;

    private static PredicadoDnf Exige(string fato) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
}

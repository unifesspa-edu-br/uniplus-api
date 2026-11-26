namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// Distribuição de vagas por oferta de curso do <see cref="ProcessoSeletivo"/>:
/// o vínculo com a raiz, as recusas de coerência entre ofertas e a concorrência dupla derivada.
/// </summary>
public sealed class ProcessoSeletivoDistribuicaoDeVagasTests
{
    [Fact(DisplayName = "DefinirDistribuicaoVagas vincula a configuração à raiz")]
    public void DefinirDistribuicaoVagas_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas configuracao = Oferta(Guid.CreateVersion7(), 50, [Ac(50)]);

        Result result = processo.DefinirDistribuicaoVagas([configuracao], FatosDeModalidadeDeTeste.DoCatalogo);

        result.IsSuccess.Should().BeTrue();
        processo.DistribuicaoVagas.Should().ContainSingle();
        processo.DistribuicaoVagas.Single().ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com AcaoQuandoIndeferido divergente para o mesmo código entre ofertas é recusada (CA-16)")]
    public void DefinirDistribuicaoVagas_AcaoQuandoIndeferidoDivergente_Falha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaA = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV("RETIRA_A_MODALIDADE", 2), Ac(8)]);
        ConfiguracaoDistribuicaoVagas ofertaB = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV(null, 2), Ac(8)]);

        Result result = processo.DefinirDistribuicaoVagas([ofertaA, ofertaB], FatosDeModalidadeDeTeste.DoCatalogo);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com a mesma modalidade copiada com regras diferentes entre ofertas é recusada")]
    public void DefinirDistribuicaoVagas_RegraDeAplicabilidadeDivergente_Falha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaA = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV(null, 2, Exige("PCD")), Ac(8)]);
        ConfiguracaoDistribuicaoVagas ofertaB = Oferta(Guid.CreateVersion7(), 10, [ModalidadeV(null, 2, Exige("CONCORRER_PCD")), Ac(8)]);

        Result result = processo.DefinirDistribuicaoVagas([ofertaA, ofertaB], FatosDeModalidadeDeTeste.DoCatalogo);

        result.IsFailure.Should().BeTrue("uma modalidade tem uma regra só no processo");
        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente);
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas vazia é recusada")]
    public void DefinirDistribuicaoVagas_Vazia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirDistribuicaoVagas([], FatosDeModalidadeDeTeste.DoCatalogo);

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
        ], FatosDeModalidadeDeTeste.DoCatalogo);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OfertaCursoDuplicada");
    }

    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é falsa sem distribuição de vagas com cota reservada")]
    public void ConcorrenciaDuplaAplicavel_SemCotaReservada_Falsa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas([Oferta(Guid.CreateVersion7(), 50, [Ac(50)])], FatosDeModalidadeDeTeste.DoCatalogo);

        RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(processo.Declaracao()).Should().BeFalse();
    }

    // Só exercita o cenário mínimo de derivação: a regra institucional evita que a matriz das
    // 8 federais + AC, validada por ConfiguracaoDistribuicaoVagas.Criar, entre no teste.
    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é derivada (true) quando há modalidade de cota reservada")]
    public void ConcorrenciaDuplaAplicavel_ComCotaReservada_Verdadeira()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas distribuicao = Oferta(Guid.CreateVersion7(), 50, [Ac(30), CotaReservadaPpi(20)]);

        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo);

        RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(processo.Declaracao()).Should().BeTrue();
    }

    private static ConfiguracaoDistribuicaoVagas Oferta(Guid ofertaCursoId, int vagasOfertadasBase, IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoId,
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = vagasOfertadasBase, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string('a', 64)), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ModalidadeSelecionada Ac(int quantidade) => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "base legal" }, quantidade).Value!;

    private static ModalidadeSelecionada ModalidadeV(string? acaoQuandoIndeferido, int quantidade, PredicadoDnf? regraDeAplicabilidade = null) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("V", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal) with { RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico, RemanejamentoDestino = "AC", RegraDeAplicabilidade = regraDeAplicabilidade ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), AcaoQuandoIndeferido = acaoQuandoIndeferido, BaseLegal = "base legal" }, quantidade).Value!;

    private static ModalidadeSelecionada CotaReservadaPpi(int quantidade) => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal) with { BaseLegal = "Lei 12.711/2012 art. 3º" }, quantidade).Value!;

    private static PredicadoDnf Exige(string fato) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
}

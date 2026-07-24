namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do <see cref="DefinirRegrasDerivacaoCommandHandler"/> (Story #985): o alvo derivado
/// com binding de regra, a semântica das condições <c>quando</c> contra o vocabulário e a oferta do
/// processo, o domínio de contribuição de MODALIDADE, e a delegação estrutural ao agregado.
/// </summary>
public sealed class DefinirRegrasDerivacaoCommandHandlerTests
{
    private const string HashFixo = "ab01234567ab01234567ab01234567ab01234567ab01234567ab01234567ab01";

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        Mocks mocks = new(repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<ISelecaoUnitOfWork>());
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(VocabularioSeed());
        return mocks;
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirRegrasDerivacaoCommand command) =>
        DefinirRegrasDerivacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

    private static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", null),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", null, "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:BAIXA_RENDA", null),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            null, "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null),
    ];

    private static ProcessoSeletivo ProcessoBase() =>
        ProcessoSeletivo.Criar("PS Regras", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

    /// <summary>Processo em rascunho que oferta a modalidade AC e coleta COR_RACA — o mínimo para uma derivação de MODALIDADE válida.</summary>
    private static ProcessoSeletivo ProcessoComModalidadeAcEColeta()
    {
        ProcessoSeletivo processo = ProcessoBase();

        processo.DefinirFatosColetados([FatoColetado.Criar("COR_RACA", 0, null).Value!])
            .IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!;
        ModalidadeSelecionada ac = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [ac]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static CondicaoDerivacaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    private static ConfiguracaoDerivacaoInput ConfigModalidade(params RegraDerivacaoInput[] regras) =>
        new("MODALIDADE", regras);

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirRegrasDerivacaoCommand(processoId, []));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Derivação de MODALIDADE válida (âncora + condicional) é aceita e persistida")]
    public async Task Handle_DerivacaoValida_DefineEPersiste()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigModalidade(
                new RegraDerivacaoInput(0, "AC", null),
                new RegraDerivacaoInput(1, "AC", [[Condicao("COR_RACA", "IGUAL", "PRETA")]])),
        ]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");
        processo.RegrasDerivacao.Should().ContainSingle(c => c.CodigoFato == "MODALIDADE");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato alvo fora do vocabulário é recusado como desconhecido")]
    public async Task Handle_AlvoDesconhecido_Retorna422()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("BOGUS", [new RegraDerivacaoInput(0, "AC", null)])]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDerivacaoFato.FatoDesconhecido");
    }

    [Fact(DisplayName = "Fato alvo declarado (não derivável) é recusado")]
    public async Task Handle_AlvoDeclarado_RetornaNaoDerivavel()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("COR_RACA", [new RegraDerivacaoInput(0, "PRETA", null)])]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDerivacaoFato.FatoNaoDerivavel");
    }

    [Fact(DisplayName = "Contribui fora das modalidades ofertadas é recusado com ContribuiForaDoDominio")]
    public async Task Handle_ContribuiForaDoDominio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // "V" não é uma modalidade ofertada (só AC é).
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigModalidade(new RegraDerivacaoInput(0, "V", null))]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegrasDerivacaoFato.ContribuiForaDoDominio");
    }

    [Fact(DisplayName = "Condição 'quando' que cita fato não disponível na configuração é recusada")]
    public async Task Handle_QuandoCitaFatoIndisponivel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // BAIXA_RENDA existe no vocabulário mas não é coletado nem derivado neste processo.
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigModalidade(new RegraDerivacaoInput(0, "AC", [[Condicao("BAIXA_RENDA", "IGUAL", true)]]))]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    [Fact(DisplayName = "Condição 'quando' com operador incompatível com o domínio do fato citado é recusada")]
    public async Task Handle_QuandoOperadorIncompativel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // COR_RACA é categórico: MAIOR_IGUAL não se aplica.
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigModalidade(new RegraDerivacaoInput(0, "AC", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]))]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }
}

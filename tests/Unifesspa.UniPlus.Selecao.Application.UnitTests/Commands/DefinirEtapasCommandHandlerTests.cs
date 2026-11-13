namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

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

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ArranjoDasEtapas;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

public sealed partial class DefinirEtapasCommandHandlerTests
{
    private static ProcessoSeletivo ProcessoComEtapa(out EtapaProcesso etapa, CaraterEtapa carater = CaraterEtapa.Classificatoria)
    {
        decimal? peso = carater is CaraterEtapa.Eliminatoria ? null : 1m;
        etapa = EtapaProcesso.Criar("Prova Objetiva", carater, TipoEtapaProvaObjetiva(), peso, ordem: 1).Value!;
        return ProcessoCom(etapa);
    }

    private static DefinirEtapasCommand RedacaoHumana(
        ProcessoSeletivo processo, string? forma, string? regraCodigo, string? regraVersao = "v1") => Definindo(
        processo,
        EtapaClassificatoria("Redação", TipoRedacaoOrigemId) with
        {
            NotaMaxima = 10m,
            PassoDoLancamento = 0.5m,
            FormaDeAvaliacao = forma,
            RegraConsolidacaoCodigo = regraCodigo,
            RegraConsolidacaoVersao = regraVersao,
        });

    private static EtapaProcessoInput EntrevistaComItens(
        Guid? id, IReadOnlyList<ItemDeAvaliacaoInput> itens, IReadOnlyList<NivelDeAtendimentoInput>? escala = null) =>
        EtapaClassificatoria("Entrevista", TipoRedacaoOrigemId, id: id) with
        {
            NotaMaxima = 10m,
            FormaDeAvaliacao = FormaDeAvaliacaoCodigo.Humana,
            ItensDeAvaliacao = itens,
            EscalaDeAtendimento = escala,
        };

    private static DefinirEtapasCommand ComUmProdutoDefinitivo(ProcessoSeletivo processo) =>
        Definindo(processo, ProvaObjetiva() with { Produtos = [Definitivo("RESULTADO_CLASSIFICACAO")] });

    /// <summary>Leitor cujo tipo de Prova Objetiva não compõe a nota final.</summary>
    private static ITipoEtapaReader ReaderComTipoQueNaoPontua() => CadastroDeTipos(
        TipoDoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false));

    private static void DeveRecusarOCaraterDaPrimeiraEtapa(Result<MutacaoAceita> result)
    {
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Field.Should().Be("etapas[0].carater");
        result.Errors[0].Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);
        CenarioDasEtapas cenario = new(repository, Substitute.For<ISelecaoUnitOfWork>());
        DefinirEtapasCommand command = new(Guid.CreateVersion7(), [ProvaObjetiva()], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O agregado é tracked (ObterParaMutacaoAsync); a persistência é por change detection no SaveChanges — NÃO
    /// se chama DbSet.Update, que marcaria os filhos novos com Guid v7 como Modified e geraria UPDATE inválido.
    /// </summary>
    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva() with { Peso = 3m });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        cenario.Repositorio.DidNotReceive().Atualizar(Arg.Any<ProcessoSeletivo>());
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Id de etapa existente atualiza a MESMA instância (preserva etapa_ref)")]
    public async Task Handle_ComIdExistente_AtualizaMesmaInstancia()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        CenarioDasEtapas cenario = Para(processo);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva("Prova Objetiva (revisada)", id: etapaOriginal.Id) with { Peso = 3m });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaAtualizada = processo.Etapas.Single();
        etapaAtualizada.Id.Should().Be(etapaOriginal.Id);
        etapaAtualizada.Should().BeSameAs(etapaOriginal);
        etapaAtualizada.Nome.Should().Be("Prova Objetiva (revisada)");
        etapaAtualizada.Peso.Should().Be(3m);
    }

    [Fact(DisplayName = "Handle sem Id (ou com Id sem correspondência) cria etapa nova")]
    public async Task Handle_SemId_CriaEtapaNova()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        CenarioDasEtapas cenario = Para(processo);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva("Entrevista") with { Peso = 2m });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "A nota máxima e o passo chegam à etapa criada e à etapa atualizada")]
    public async Task Handle_Escala_ChegaACriadaEAAtualizada()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso existente);
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput atualizada = ProvaObjetiva(id: existente.Id) with { NotaMaxima = 10m, PassoDoLancamento = 0.25m };
        EtapaProcessoInput criada = ProvaObjetiva("Redação", ordem: 2) with { NotaMaxima = 1000m, PassoDoLancamento = 20m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, atualizada, criada));

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Single(e => e.Id == existente.Id).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 10m && e.PassoDoLancamento == 0.25m);
        processo.Etapas.Single(e => e.Nome == "Redação").Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 1000m && e.PassoDoLancamento == 20m);
    }

    [Fact(DisplayName = "Defeitos de escala em etapas diferentes saem numa só resposta, cada um no seu campo")]
    public async Task Handle_DefeitosDeEscalaEmEtapasDiferentes_Acumula()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput semNotaMaxima = ProvaObjetiva() with { NotaMaxima = 0m };
        EtapaProcessoInput comNotaMinimaAcimaDaMaxima = EtapaEliminatoria("Entrevista", TipoProvaObjetivaOrigemId, 2, 12m) with { NotaMaxima = 10m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, semNotaMaxima, comNotaMinimaAcimaDaMaxima));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].notaMaxima", "etapas[1].notaMinima"]);
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput duplicada = ProvaObjetiva("Prova Objetiva (duplicada)", ordem: 2, id: etapaOriginal.Id) with { Peso = 2m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaObjetiva(id: etapaOriginal.Id), duplicada));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput redacao = EtapaClassificatoria("Redação", TipoRedacaoOrigemId) with { Peso = 2m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaObjetiva() with { Peso = 3m }, redacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}

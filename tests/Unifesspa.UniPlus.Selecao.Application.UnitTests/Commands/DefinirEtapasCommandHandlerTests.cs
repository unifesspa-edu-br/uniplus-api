namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirEtapasCommandHandlerTests
{
    private static readonly Guid TipoProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    private static readonly Guid TipoRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");

    /// <summary>Resolve os dois tipos usados nos testes deste arquivo — Prova Objetiva e Redação.</summary>
    private static ITipoEtapaReader ReaderPadrao()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null, true, true));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null, true, true));
        return reader;
    }

    private static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva").Value!;

    /// <summary>
    /// A fase confere o ato contra o catálogo de Publicações; a etapa não conferia. O código
    /// declarado aqui vira o nome do produto no cronograma público e a chave pela qual a
    /// restauração da configuração congelada reencontra o produto — um código com erro de
    /// digitação só apareceria depois de o certame estar no Diário Oficial.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com ato fora do catálogo é recusado, como na fase")]
    public async Task Handle_ProdutoComAtoForaDoCatalogo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("RESULTADO_PRELMINAR", null)]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, ReaderPadrao(), Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), tipoAtoPublicadoReader, TimeProvider.System,
            unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Papel preliminar ou definitivo é o par que a janela recursal ancora, e só resultado o
    /// admite. A fase recusa o mesmo.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com papel sobre ato que não é resultado é recusado")]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                "EDITAL_ABERTURA", "Edital de abertura", CongelaConfiguracao: true, UnicoPorObjeto: true,
                EfeitoIrreversivel: false, EhResultado: false));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("EDITAL_ABERTURA", "PRELIMINAR")]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, ReaderPadrao(), Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), tipoAtoPublicadoReader, TimeProvider.System,
            unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static TipoAtoPublicadoView AtoDeResultado(string codigo) => new(
        codigo, codigo, CongelaConfiguracao: false, UnicoPorObjeto: false,
        EfeitoIrreversivel: false, EhResultado: true);

    private static ProcessoSeletivo ProcessoComEtapa(out EtapaProcesso etapa, CaraterEtapa carater = CaraterEtapa.Classificatoria)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        decimal? peso = carater is CaraterEtapa.Eliminatoria ? null : 1m;
        etapa = EtapaProcesso.Criar("Prova Objetiva", carater, TipoEtapaProvaObjetiva(), peso, ordem: 1).Value!;
        // Falha aqui é fixture inválida, não cenário: sem a asserção o processo voltaria sem
        // etapa nenhuma e o teste morreria depois, longe da causa.
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue("a etapa da fixture tem de ser aceita pelo agregado");
        return processo;
    }

    /// <summary>Leitor cujo tipo de Prova Objetiva não compõe a nota final.</summary>
    private static ITipoEtapaReader ReaderComTipoQueNaoPontua()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(
                TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null,
                AdmitePontuacao: false, AdmiteEliminacao: true));
        return reader;
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            Guid.CreateVersion7(),
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        // O agregado é tracked (ObterParaMutacaoAsync); a persistência é por
        // change detection no SaveChanges — NÃO se chama DbSet.Update (que
        // marcaria os filhos novos com Guid v7 como Modified → UPDATE inválido).
        repository.DidNotReceive().Atualizar(Arg.Any<Domain.Entities.ProcessoSeletivo>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Id de etapa existente atualiza a MESMA instância (preserva etapa_ref)")]
    public async Task Handle_ComIdExistente_AtualizaMesmaInstancia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id),
                new EtapaProcessoInput("Prova Objetiva (duplicada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 2, etapaOriginal.Id),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 2m, null, 1),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): a primeira etapa do payload (vínculo
    /// inalterado) não pode ter sido mutada quando a SEGUNDA é recusada por tipo inativo: o
    /// SaveChangesAsync automático do Wolverine persistiria a mutação parcial mesmo com o PUT
    /// inteiro recusado. A resolução dos tipos acontece na passada que confere o caráter, antes
    /// de a reconciliação tocar qualquer instância rastreada — então não há mutação parcial a
    /// desfazer, e não há rastreamento a descartar.
    /// </summary>
    [Fact(DisplayName = "Handle com tipo inativo numa etapa posterior recusa sem mutar a anterior")]
    public async Task Handle_ComTipoInativoNaEtapaPosterior_RecusaSemMutarAAnterior()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        Guid tipoInativo = Guid.CreateVersion7();
        tipoEtapaReader.ObterAtivoPorIdAsync(tipoInativo, Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                // Vínculo e caráter inalterados: só o peso muda, e a etapa é a instância tracked.
                new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id),
                // Etapa nova com tipo inativo — é ela que derruba o PUT inteiro.
                new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, tipoInativo, 1m, null, 2),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        etapaOriginal.Nome.Should().Be("Prova Objetiva", "a etapa anterior não pode ter sido mutada");
        etapaOriginal.Peso.Should().Be(1m);
        unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): desativar um tipo já vinculado a uma etapa
    /// não pode bloquear PUTs subsequentes da coleção inteira — só vínculos NOVOS ou que MUDAM
    /// de tipo precisam do cadastro ativo. O snapshot já congelado é reaproveitado sem nova
    /// leitura do cadastro.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado reaproveita o snapshot já congelado, mesmo com o tipo desde então desativado")]
    public async Task Handle_ComVinculoInalterado_ReaproveitaSnapshotMesmoComTipoDesativado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Reader configurado para devolver null para TODO Id — simula o tipo desativado depois
        // que a etapa já existia. Se o handler tentasse reavaliar o vínculo inalterado, a
        // publicação inteira recusaria com TipoEtapaNaoEncontradoOuInativo.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Edita só o Peso — TipoEtapaOrigemId permanece o mesmo do snapshot já congelado.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(9m);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): o snapshot-copy (ADR-0061) só muda quando o
    /// VÍNCULO muda — renomear o tipo no cadastro, ainda ativo, não pode reescrever
    /// silenciosamente o snapshot de uma etapa que não trocou de TipoEtapaOrigemId.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado não atualiza o Nome congelado, mesmo com o tipo renomeado no cadastro")]
    public async Task Handle_ComVinculoInalterado_NaoAtualizaNomeAoRenomearTipoNoCadastro()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Se o handler fosse reler o cadastro para um vínculo inalterado, encontraria este Nome
        // renomeado — a asserção abaixo prova que ele nunca chega a consultar.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", null, true, true));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Nome.Should().Be("Prova Objetiva",
            "o snapshot só muda quando o VÍNCULO (TipoEtapaOrigemId) muda, não quando o cadastro de origem muda");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Handle com vínculo alterado resolve o novo tipo contra o cadastro corrente")]
    public async Task Handle_ComVinculoAlterado_ResolveContraCadastroAtual()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Mesma etapa (mesmo Id), agora vinculada à Redação em vez de Prova Objetiva.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("REDACAO");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): TipoEtapa é owned type (OwnsOne) de
    /// EtapaProcesso — EF Core não aceita a MESMA instância de TipoEtapaSnapshot pertencendo a
    /// duas etapas ao mesmo tempo. Duas etapas novas com o mesmo tipo é caso legítimo (duas
    /// fases classificatórias do mesmo propósito institucional); o cache de resolução tem de
    /// reaproveitar só o dado do catálogo, nunca a instância do snapshot já construído.
    /// </summary>
    [Fact(DisplayName = "Handle com duas etapas novas do mesmo tipo cria instâncias distintas de TipoEtapaSnapshot")]
    public async Task Handle_ComDuasEtapasMesmoTipo_CriaInstanciasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova — Manhã", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
                new EtapaProcessoInput("Prova — Tarde", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        EtapaProcesso[] etapas = [.. processo.Etapas.OrderBy(e => e.Ordem)];
        etapas.Should().HaveCount(2);
        etapas[0].TipoEtapa.Should().NotBeSameAs(etapas[1].TipoEtapa,
            "TipoEtapa é owned type — a mesma instância em dois donos quebraria o change tracking do EF Core");
        etapas[0].TipoEtapa.Should().Be(etapas[1].TipoEtapa, "mesmo conteúdo, apesar de instâncias diferentes (record com igualdade por valor)");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "issue #1071 — CA-03: tipo de etapa inexistente ou inativo é recusado")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Guid tipoInexistente = Guid.CreateVersion7();
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, tipoInexistente, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        processo.Etapas.Should().BeEmpty("nenhuma etapa pode ser persistida sem snapshot de tipo (CA-04)");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        // Nada a descartar: os tipos do payload são resolvidos na passada que confere o caráter,
        // antes de a reconciliação tocar qualquer instância rastreada.
        unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "ADR-0125: violações de forma acumulam entre etapas, com o índice prefixado ao field, ANTES de consultar o tipoEtapaReader")]
    public async Task Handle_DuasEtapasComViolacaoDeForma_AcumulaComIndicePrefixadoESemConsultarReader()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(" ", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 0m, null, 2),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, tipoBancaReader, regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].nome", "etapas[1].peso"]);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A etapa publica o mesmo ato duas vezes — preliminar e definitivo —, e é da publicação
    /// preliminar que a janela recursal corre. Resolver a âncora só pelo código do ato devolvia
    /// o definitivo sempre que ele viesse primeiro na coleção, e a etapa era recusada por
    /// ancorar onde não se pode — sem que nada no payload estivesse errado.
    /// </summary>
    [Fact(DisplayName = "A âncora do recurso resolve no produto PRELIMINAR, mesmo com o definitivo antes na coleção")]
    public async Task Handle_AncoraComDefinitivoAntesDoPreliminar_ResolveNoPreliminar()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        Result<RegraCatalogo> regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
            "Lei 9.784/1999, art. 59");
        regra.IsSuccess.Should().BeTrue(regra.Error?.Message);
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_PRELIMINAR"));
        regraCatalogoReader
            .ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regra.Value!);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos:
                    [
                        // O definitivo vem primeiro de propósito: é a ordem que o servidor
                        // devolve depois de reler a etapa, e era ela que derrubava a gravação.
                        new ProdutoDaEtapaInput("RESULTADO_PRELIMINAR", "DEFINITIVO"),
                        new ProdutoDaEtapaInput("RESULTADO_PRELIMINAR", "PRELIMINAR"),
                    ],
                    Recursos:
                    [
                        new RecursoDaEtapaInput(
                            AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                            2m, UnidadePrazo.DiasUteis, "RESULTADO_PRELIMINAR", null, null, null, null),
                    ]),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, ReaderPadrao(), Substitute.For<ITipoBancaReader>(),
            regraCatalogoReader, tipoAtoPublicadoReader, TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    /// <summary>
    /// Trocar SÓ o caráter de uma etapa que já existia é o caminho em que o tipo não seria
    /// reconsultado por reconciliação — o vínculo não mudou —, e por onde um caráter que o
    /// cadastro deixou de admitir entraria sem ninguém conferir.
    /// </summary>
    [Fact(DisplayName = "Handle que só troca o caráter confere contra o que o tipo admite")]
    public async Task Handle_TrocaApenasOCarater_ConfereContraOCadastro()
    {
        // A etapa nasceu acumulando os dois papéis, quando o tipo ainda compunha a nota final.
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal, CaraterEtapa.Ambas);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id)],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, ReaderComTipoQueNaoPontua(), Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), Substitute.For<ITipoAtoPublicadoReader>(), TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Field.Should().Be("etapas[0].carater");
        result.Errors[0].Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas, "a recusa não muta o agregado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Estreitar um tipo ainda ativo — declarar que ele deixou de compor a nota final — é a
    /// operação que o cadastro existe para permitir. Ela não pode passar a recusar todo PUT
    /// posterior de um certame que já tenha etapa daquele tipo: o que estava declarado
    /// permanece, e a conferência alcança só o que a pessoa está declarando agora.
    /// </summary>
    [Fact(DisplayName = "Handle que preserva vínculo e caráter não reconsulta o cadastro nem recusa por estreitamento")]
    public async Task Handle_VinculoECaraterInalterados_NaoReconsultaOCadastro()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderComTipoQueNaoPontua();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Edita só o peso; o caráter classificatório continua o mesmo que o tipo passou a recusar.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 7m, null, 1, etapaOriginal.Id)],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, tipoEtapaReader, Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), Substitute.For<ITipoAtoPublicadoReader>(), TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(7m);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// Quem errou o tipo de uma etapa e o caráter de outra corrige as duas de uma vez. Sair na
    /// primeira recusa esconderia a segunda até a tentativa seguinte.
    /// </summary>
    [Fact(DisplayName = "Handle acumula tipo inativo e caráter não admitido na mesma resposta")]
    public async Task Handle_TipoInativoECaraterNaoAdmitido_AcumulaOsDois()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        Guid tipoInativo = Guid.CreateVersion7();
        ITipoEtapaReader reader = ReaderComTipoQueNaoPontua();
        reader.ObterAtivoPorIdAsync(tipoInativo, Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Análise", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
                new EtapaProcessoInput("Entrevista", CaraterEtapa.Eliminatoria, tipoInativo, null, 5m, 2),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, reader, Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), Substitute.For<ITipoAtoPublicadoReader>(), TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal(
            "etapas[0].carater", "etapas[1].tipoEtapaOrigemId");
    }

    [Fact(DisplayName = "Handle acumula a recusa de caráter das duas etapas do payload")]
    public async Task Handle_DuasEtapasComCaraterNaoAdmitido_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Análise 1", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
                new EtapaProcessoInput("Análise 2", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 2m, 5m, 2),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, ReaderComTipoQueNaoPontua(), Substitute.For<ITipoBancaReader>(),
            Substitute.For<IRegraCatalogoReader>(), Substitute.For<ITipoAtoPublicadoReader>(), TimeProvider.System, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal("etapas[0].carater", "etapas[1].carater");
    }
}

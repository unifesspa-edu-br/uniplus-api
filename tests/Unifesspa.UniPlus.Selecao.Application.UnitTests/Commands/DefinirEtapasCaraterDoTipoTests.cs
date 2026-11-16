namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasCaraterDoTipoTests
{
    private static ProcessoSeletivo ProcessoComEtapa(out EtapaProcesso etapa, CaraterEtapa carater = CaraterEtapa.Classificatoria)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        decimal? peso = carater is CaraterEtapa.Eliminatoria ? null : 1m;
        etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: carater, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
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
                AdmitePontuacao: false, AdmiteEliminacao: true, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));
        return reader;
    }

    /// <summary>
    /// Trocar SÓ o caráter de uma etapa que já existia é o caminho em que o tipo não seria
    /// reconsultado por reconciliação — o vínculo não mudou —, e por onde um caráter que o
    /// cadastro deixou de admitir entraria sem ninguém conferir.
    /// </summary>
    [Fact(DisplayName = "Handle que só troca o caráter confere contra o que o tipo ativo admite hoje")]
    public async Task Handle_TrocaApenasOCarater_ConfereContraOTipoAtivo()
    {
        // A etapa nasceu acumulando os dois papéis, quando o tipo ainda compunha a nota final.
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal, CaraterEtapa.Ambas);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, ReaderComTipoQueNaoPontua());

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Field.Should().Be("etapas[0].carater");
        result.Errors[0].Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas, "a recusa não muta o agregado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O tipo passou a admitir pontuação depois de a etapa existir, e o operador torna a
    /// etapa classificatória. A gravação é aceita e o snapshot passa a
    /// declarar o que o tipo admite agora — com a identidade do snapshot antigo, nunca a do
    /// cadastro.
    /// </summary>
    [Fact(DisplayName = "Alargar o tipo alcança quem muda o caráter, e o snapshot regrava só os sinalizadores")]
    public async Task Handle_TipoAlargadoECaraterAlterado_RegravaSinalizadoresComIdentidadeAntiga()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoEtapaSnapshot semPontuacao = TipoEtapaSnapshot.Criar(
            TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso eliminatoria = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: semPontuacao, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso redacao = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(TipoRedacaoOrigemId, "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([eliminatoria, redacao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // O cadastro, hoje: admite pontuação — e foi renomeado depois do congelamento.
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", null, AdmitePontuacao: true, AdmiteEliminacao: false, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1, eliminatoria.Id, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 2, redacao.Id, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        TipoEtapaSnapshot regravado = processo.Etapas.Single(e => e.Id == eliminatoria.Id).TipoEtapa;
        regravado.AdmitePontuacao.Should().BeTrue();
        regravado.AdmiteEliminacao.Should().BeFalse();
        regravado.Nome.Should().Be("Prova Objetiva", "a identidade vem do snapshot antigo, não do cadastro renomeado");
        regravado.OrigemId.Should().Be(TipoProvaObjetivaOrigemId);
    }

    /// <summary>
    /// Etapa nova congela o que o tipo admite no cadastro no momento da gravação. Os dois tipos
    /// declaram pares opostos, então trocar um sinalizador pelo outro no congelamento não passa.
    /// </summary>
    [Fact(DisplayName = "Etapa nova congela no snapshot os sinalizadores lidos do cadastro")]
    public async Task Handle_EtapaNova_CongelaSinalizadoresDoCadastro()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null, AdmitePontuacao: true, AdmiteEliminacao: false, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null, AdmitePontuacao: false, AdmiteEliminacao: true, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 5m, 2, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        TipoEtapaSnapshot redacao = processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoRedacaoOrigemId).TipoEtapa;
        redacao.AdmitePontuacao.Should().BeTrue();
        redacao.AdmiteEliminacao.Should().BeFalse();
        TipoEtapaSnapshot prova = processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoProvaObjetivaOrigemId).TipoEtapa;
        prova.AdmitePontuacao.Should().BeFalse();
        prova.AdmiteEliminacao.Should().BeTrue();
    }

    /// <summary>
    /// O cache de tipos é compartilhado entre as etapas do payload. Mudar o caráter de uma etapa
    /// lê o tipo e regrava o snapshot DELA — a outra etapa do mesmo tipo, que não mudou nada,
    /// mantém o que congelou.
    /// </summary>
    [Fact(DisplayName = "Mudar o caráter de uma etapa não refresca o snapshot de outra do mesmo tipo")]
    public async Task Handle_DuasEtapasDoMesmoTipo_SoARegravadaMudaDeSinalizadores()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoEtapaSnapshot congelado = TipoEtapaSnapshot.Criar(
            TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso primeira = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Objetiva I", Carater: CaraterEtapa.Classificatoria, TipoEtapa: congelado, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso segunda = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Objetiva II", Carater: CaraterEtapa.Classificatoria, TipoEtapa: congelado.ComSinalizadores(true, true), Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([primeira, segunda], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Objetiva I", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, primeira.Id, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Objetiva II", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 5m, 2, segunda.Id, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        // O cadastro deixou de admitir pontuação; a eliminação continua admitida.
        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, ReaderComTipoQueNaoPontua());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single(e => e.Id == segunda.Id).TipoEtapa.AdmitePontuacao.Should().BeFalse("o caráter dela mudou e o tipo foi relido");
        processo.Etapas.Single(e => e.Id == primeira.Id).TipoEtapa.AdmitePontuacao.Should().BeTrue("ela não mudou nada, e o cache compartilhado não a alcança");
    }

    /// <summary>
    /// Tipo desativado e vínculo inalterado: não há o que ler no cadastro, mas o que o tipo
    /// admitia está congelado na etapa — o caráter novo é conferido contra o snapshot.
    /// </summary>
    [Theory(DisplayName = "Com o tipo desativado, trocar o caráter confere contra o snapshot, sem recusar pelo tipo inativo")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Handle_TipoDesativadoECaraterAlterado_ConfereContraOSnapshot(bool snapshotAdmiteEliminacao, bool aceito)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: snapshotAdmiteEliminacao, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 1m, 5m, 1, etapa.Id, Produtos: [], Bancas: [], Recursos: [])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        if (aceito)
        {
            result.IsSuccess.Should().BeTrue(result.Error?.Message);
            processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas);
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle();
            result.Errors[0].Field.Should().Be("etapas[0].carater");
            result.Errors[0].Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        }

        result.Errors.Should().NotContain(e => e.Error.Code == "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
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
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // Edita só o peso; o caráter classificatório continua o mesmo que o tipo passou a recusar.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 7m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

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
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        Guid tipoInativo = Guid.CreateVersion7();
        ITipoEtapaReader reader = ReaderComTipoQueNaoPontua();
        reader.ObterAtivoPorIdAsync(tipoInativo, Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Análise", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Entrevista", CaraterEtapa.Eliminatoria, tipoInativo, null, 5m, 2, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal(
            "etapas[0].carater", "etapas[1].tipoEtapaOrigemId");
    }

    [Fact(DisplayName = "Handle acumula a recusa de caráter das duas etapas do payload")]
    public async Task Handle_DuasEtapasComCaraterNaoAdmitido_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Análise 1", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Análise 2", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 2m, 5m, 2, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, ReaderComTipoQueNaoPontua());

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal("etapas[0].carater", "etapas[1].carater");
    }
}

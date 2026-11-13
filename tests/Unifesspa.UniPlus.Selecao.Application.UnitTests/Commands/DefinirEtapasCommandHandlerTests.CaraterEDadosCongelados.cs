namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ArranjoDasEtapas;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

public sealed partial class DefinirEtapasCommandHandlerTests
{
    private static (ProcessoSeletivo Processo, EtapaProcesso Eliminatoria, EtapaProcesso Redacao) ProcessoComEliminatoriaSemPontuacaoERedacao()
    {
        TipoEtapaSnapshot semPontuacao = SnapshotDoTipo(
            TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: true);
        TipoEtapaSnapshot daRedacao = SnapshotDoTipo(TipoRedacaoOrigemId, "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true);
        EtapaProcesso eliminatoria = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Eliminatoria, semPontuacao, null, notaMinima: 5m, ordem: 1).Value!;
        EtapaProcesso redacao = EtapaClassificatoriaDoAgregado("Redação", 2, daRedacao);
        return (ProcessoCom(eliminatoria, redacao), eliminatoria, redacao);
    }

    private static ITipoEtapaReader CadastroQueSeparaPontuacaoDeEliminacao() => CadastroDeTipos(
        TipoDoCadastro(TipoRedacaoOrigemId, "REDACAO", "Redação", admiteEliminacao: false),
        TipoDoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false));

    private static ITipoEtapaReader CadastroComANotaDoEnemNaProva() => CadastroDeTipos(
        TipoDoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", notaDeOrigemNoEnem: true));

    private static (CenarioDasEtapas Cenario, ProcessoSeletivo Processo, DefinirEtapasCommand Command) TipoDesativadoComCaraterAlterado(
        bool snapshotAdmiteEliminacao)
    {
        TipoEtapaSnapshot congelado = SnapshotDoTipo(
            TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: snapshotAdmiteEliminacao);
        EtapaProcesso etapa = EtapaClassificatoriaDoAgregado("Prova Objetiva", 1, congelado);
        ProcessoSeletivo processo = ProcessoCom(etapa);
        EtapaProcessoInput ambas = ProvaObjetiva(id: etapa.Id) with { Carater = CaraterEtapa.Ambas, NotaMinima = 5m };
        return (Para(processo, tiposDeEtapa: CadastroSemTipoAtivo()), processo, Definindo(processo, ambas));
    }

    /// <summary>
    /// Trocar SÓ o caráter de uma etapa que já existia é o caminho em que o tipo não seria
    /// reconsultado por reconciliação — o vínculo não mudou —, e por onde um caráter que o
    /// cadastro deixou de admitir entraria sem ninguém conferir. A etapa nasceu acumulando os
    /// dois papéis, quando o tipo ainda compunha a nota final.
    /// </summary>
    [Fact(DisplayName = "Handle que só troca o caráter confere contra o que o tipo ativo admite hoje")]
    public async Task Handle_TrocaApenasOCarater_ConfereContraOTipoAtivo()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal, CaraterEtapa.Ambas);
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: ReaderComTipoQueNaoPontua());
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva(id: etapaOriginal.Id));

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        DeveRecusarOCaraterDaPrimeiraEtapa(result);
        processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas, "a recusa não muta o agregado");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O tipo passou a admitir pontuação depois de a etapa existir, e o operador torna a
    /// etapa classificatória. A gravação é aceita e o snapshot passa a
    /// declarar o que o tipo admite agora — com a identidade do snapshot antigo, nunca a do
    /// cadastro, que hoje admite pontuação e foi renomeado depois do congelamento.
    /// </summary>
    [Fact(DisplayName = "Alargar o tipo alcança quem muda o caráter, e o snapshot regrava só os sinalizadores")]
    public async Task Handle_TipoAlargadoECaraterAlterado_RegravaSinalizadoresComIdentidadeAntiga()
    {
        (ProcessoSeletivo processo, EtapaProcesso eliminatoria, EtapaProcesso redacao) = ProcessoComEliminatoriaSemPontuacaoERedacao();
        ITipoEtapaReader cadastro = CadastroDeTipos(TipoDoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", admiteEliminacao: false));
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        EtapaProcessoInput provaRevisada = ProvaObjetiva(id: eliminatoria.Id) with { Peso = 2m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(
            Definindo(processo, provaRevisada, EtapaClassificatoria("Redação", TipoRedacaoOrigemId, 2, redacao.Id)));

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
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: CadastroQueSeparaPontuacaoDeEliminacao());
        EtapaProcessoInput prova = EtapaEliminatoria("Prova Objetiva", TipoProvaObjetivaOrigemId, 2);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(
            Definindo(processo, EtapaClassificatoria("Redação", TipoRedacaoOrigemId), prova));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        TipoEtapaSnapshot redacao = processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoRedacaoOrigemId).TipoEtapa;
        redacao.AdmitePontuacao.Should().BeTrue();
        redacao.AdmiteEliminacao.Should().BeFalse();
        TipoEtapaSnapshot provaCongelada = processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoProvaObjetivaOrigemId).TipoEtapa;
        provaCongelada.AdmitePontuacao.Should().BeFalse();
        provaCongelada.AdmiteEliminacao.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com recurso em ato é recusada pela regra do ENEM, antes da conferência da âncora")]
    public async Task Handle_NotaEnemComRecursoEmAto_RecusaPelaRegraDoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, regras: CatalogoDoPrazoAncoradoEmAto(), tiposDeEtapa: CadastroComANotaDoEnemNaProva());
        EtapaProcessoInput etapa = ProvaObjetiva("Nota do ENEM") with { Recursos = [RecursoAncoradoNoAto(AtoDaClassificacao)] };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, etapa));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Etapa nova congela a nota de origem no ENEM lida do cadastro")]
    public async Task Handle_EtapaNova_CongelaANotaDeOrigemNoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader cadastro = CadastroDeTipos(
            TipoDoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", notaDeOrigemNoEnem: true),
            TipoDoCadastro(TipoRedacaoOrigemId, "REDACAO", "Redação"));
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        EtapaProcessoInput redacao = EtapaClassificatoria("Redação", TipoRedacaoOrigemId, 2);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaObjetiva("Nota do ENEM"), redacao));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoProvaObjetivaOrigemId).DeclaraNotaDoEnem.Should().BeTrue();
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoRedacaoOrigemId).DeclaraNotaDoEnem.Should().BeFalse();
    }

    /// <summary>
    /// A origem da nota é identidade: mudar o caráter da etapa relê o tipo para regravar os
    /// sinalizadores, e a origem congelada fica como estava, seja o que for que o cadastro diga.
    /// </summary>
    [Fact(DisplayName = "Mudar o caráter regrava os sinalizadores e mantém a nota de origem no ENEM congelada")]
    public async Task Handle_CaraterAlterado_MantemANotaDeOrigemNoEnemCongelada()
    {
        TipoEtapaSnapshot doEnem = SnapshotDoTipo(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", true, true, notaDeOrigemNoEnem: true);
        EtapaProcesso etapa = EtapaClassificatoriaDoAgregado("Nota do ENEM", 1, doEnem);
        ProcessoSeletivo processo = ProcessoCom(etapa);
        ITipoEtapaReader cadastro = CadastroDeTipos(TipoDoCadastro(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM"));
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        EtapaProcessoInput ambas = ProvaObjetiva("Nota do ENEM", id: etapa.Id) with { Carater = CaraterEtapa.Ambas, NotaMinima = 5m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ambas));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.NotaDeOrigemNoEnem.Should().BeTrue(
            "a regravação dos sinalizadores não alcança a identidade congelada");
        await cadastro.Received().ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O cache de tipos é compartilhado entre as etapas do payload. Mudar o caráter de uma etapa
    /// lê o tipo e regrava o snapshot DELA — a outra etapa do mesmo tipo, que não mudou nada,
    /// mantém o que congelou. O cadastro deixou de admitir pontuação; a eliminação continua admitida.
    /// </summary>
    [Fact(DisplayName = "Mudar o caráter de uma etapa não refresca o snapshot de outra do mesmo tipo")]
    public async Task Handle_DuasEtapasDoMesmoTipo_SoARegravadaMudaDeSinalizadores()
    {
        TipoEtapaSnapshot congelado = TipoEtapaProvaObjetiva();
        EtapaProcesso primeira = EtapaClassificatoriaDoAgregado("Objetiva I", 1, congelado);
        EtapaProcesso segunda = EtapaClassificatoriaDoAgregado("Objetiva II", 2, congelado.ComSinalizadores(true, true));
        ProcessoSeletivo processo = ProcessoCom(primeira, segunda);
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: ReaderComTipoQueNaoPontua());
        EtapaProcessoInput segundaEliminatoria = EtapaEliminatoria("Objetiva II", TipoProvaObjetivaOrigemId, 2, id: segunda.Id);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(
            Definindo(processo, ProvaObjetiva("Objetiva I", id: primeira.Id), segundaEliminatoria));

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
        (CenarioDasEtapas cenario, ProcessoSeletivo processo, DefinirEtapasCommand command) = TipoDesativadoComCaraterAlterado(snapshotAdmiteEliminacao);
        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        if (aceito)
        {
            result.IsSuccess.Should().BeTrue(result.Error?.Message);
            processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas);
        }
        else
        {
            DeveRecusarOCaraterDaPrimeiraEtapa(result);
        }

        result.Errors.Should().NotContain(e => e.Error.Code == "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
    }

    /// <summary>
    /// Estreitar um tipo ainda ativo — declarar que ele deixou de compor a nota final — é a
    /// operação que o cadastro existe para permitir. Ela não pode passar a recusar todo PUT
    /// posterior de um certame que já tenha etapa daquele tipo: o que estava declarado
    /// permanece, e a conferência alcança só o que a pessoa está declarando agora. Aqui só o peso
    /// muda; o caráter classificatório continua o mesmo que o tipo passou a recusar.
    /// </summary>
    [Fact(DisplayName = "Handle que preserva vínculo e caráter não reconsulta o cadastro nem recusa por estreitamento")]
    public async Task Handle_VinculoECaraterInalterados_NaoReconsultaOCadastro()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        ITipoEtapaReader cadastro = ReaderComTipoQueNaoPontua();
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva(id: etapaOriginal.Id) with { Peso = 7m });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(7m);
        await cadastro.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// Quem errou o tipo de uma etapa e o caráter de outra corrige as duas de uma vez. Sair na
    /// primeira recusa esconderia a segunda até a tentativa seguinte.
    /// </summary>
    [Fact(DisplayName = "Handle acumula tipo inativo e caráter não admitido na mesma resposta")]
    public async Task Handle_TipoInativoECaraterNaoAdmitido_AcumulaOsDois()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: ReaderComTipoQueNaoPontua());
        EtapaProcessoInput entrevistaComTipoInativo = EtapaEliminatoria("Entrevista", Guid.CreateVersion7(), 2);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaObjetiva("Análise"), entrevistaComTipoInativo));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal(
            "etapas[0].carater", "etapas[1].tipoEtapaOrigemId");
    }

    [Fact(DisplayName = "Handle acumula a recusa de caráter das duas etapas do payload")]
    public async Task Handle_DuasEtapasComCaraterNaoAdmitido_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: ReaderComTipoQueNaoPontua());
        EtapaProcessoInput ambas = ProvaObjetiva("Análise 2", 2) with { Carater = CaraterEtapa.Ambas, Peso = 2m, NotaMinima = 5m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaObjetiva("Análise 1"), ambas));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(erro => erro.Field).Should().Equal("etapas[0].carater", "etapas[1].carater");
    }
}

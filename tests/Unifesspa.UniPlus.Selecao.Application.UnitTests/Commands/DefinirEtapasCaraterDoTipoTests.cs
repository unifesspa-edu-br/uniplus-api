namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasCaraterDoTipoTests
{
    private static ProcessoSeletivo ProcessoComEtapa(out EtapaProcesso etapa, CaraterEtapa carater = CaraterEtapa.Classificatoria)
    {
        decimal? peso = carater is CaraterEtapa.Eliminatoria ? null : 1m;
        etapa = EtapaDoProcesso("Prova Objetiva", carater, 1, TipoEtapaProvaObjetiva(), peso);

        return ProcessoComAsEtapas(etapa);
    }

    /// <summary>Leitor cujo tipo de Prova Objetiva não compõe a nota final.</summary>
    private static ITipoEtapaReader ReaderComTipoQueNaoPontua()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(TipoNoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: true));
        return reader;
    }

    /// <summary>Leitor cujo tipo de Prova Objetiva, já renomeado no cadastro, passou a compor a nota final.</summary>
    private static ITipoEtapaReader ReaderComTipoRenomeadoQueAgoraPontua() =>
        ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", admitePontuacao: true, admiteEliminacao: false));

    private static (bool AdmitePontuacao, bool AdmiteEliminacao) SinalizadoresCongelados(ProcessoSeletivo processo, Guid tipoEtapaOrigemId)
    {
        TipoEtapaSnapshot tipo = processo.Etapas.Single(e => e.TipoEtapaOrigemId == tipoEtapaOrigemId).TipoEtapa;
        return (tipo.AdmitePontuacao, tipo.AdmiteEliminacao);
    }

    private static TipoEtapaSnapshot ProvaObjetivaCongelada(bool admitePontuacao, bool admiteEliminacao) =>
        TipoCongelado(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao, admiteEliminacao);

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
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            processo.Id, [EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id)]);

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork, ReaderComTipoQueNaoPontua());

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].carater");
        result.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.CaraterNaoAdmitidoPeloTipo);
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
        EtapaProcesso eliminatoria = EtapaDoProcesso("Prova Objetiva", CaraterEtapa.Eliminatoria, 1, ProvaObjetivaCongelada(false, true), null, 5m);
        EtapaProcesso redacao = EtapaDoProcesso("Redação", CaraterEtapa.Classificatoria, 2, TipoCongelado(TipoRedacaoOrigemId, "REDACAO", "Redação", true, true), 1m);
        ProcessoSeletivo processo = ProcessoComAsEtapas(eliminatoria, redacao);

        Result result = await ExecutarSobre(
            processo, ReaderComTipoRenomeadoQueAgoraPontua(),
            EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1, eliminatoria.Id),
            EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 2, redacao.Id));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        TipoEtapaSnapshot regravado = processo.Etapas.Single(e => e.Id == eliminatoria.Id).TipoEtapa;
        (regravado.AdmitePontuacao, regravado.AdmiteEliminacao).Should().Be((true, false));
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
        ITipoEtapaReader reader = ReaderDoTipo(
            TipoNoCadastro(TipoRedacaoOrigemId, "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: false),
            TipoNoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: true));

        Result result = await ExecutarSobre(
            processo, reader,
            EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1),
            EtapaInput("Prova Objetiva", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 5m, 2));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        SinalizadoresCongelados(processo, TipoRedacaoOrigemId).Should().Be((true, false));
        SinalizadoresCongelados(processo, TipoProvaObjetivaOrigemId).Should().Be((false, true));
    }

    /// <summary>
    /// O cache de tipos é compartilhado entre as etapas do payload. Mudar o caráter de uma etapa
    /// lê o tipo e regrava o snapshot DELA — a outra etapa do mesmo tipo, que não mudou nada,
    /// mantém o que congelou.
    /// </summary>
    [Fact(DisplayName = "Mudar o caráter de uma etapa não refresca o snapshot de outra do mesmo tipo")]
    public async Task Handle_DuasEtapasDoMesmoTipo_SoARegravadaMudaDeSinalizadores()
    {
        TipoEtapaSnapshot congelado = ProvaObjetivaCongelada(true, true);
        EtapaProcesso primeira = EtapaDoProcesso("Objetiva I", CaraterEtapa.Classificatoria, 1, congelado, 1m);
        EtapaProcesso segunda = EtapaDoProcesso("Objetiva II", CaraterEtapa.Classificatoria, 2, congelado.ComSinalizadores(true, true, false), 1m);
        ProcessoSeletivo processo = ProcessoComAsEtapas(primeira, segunda);

        // O cadastro deixou de admitir pontuação; a eliminação continua admitida.
        Result result = await ExecutarSobre(
            processo, ReaderComTipoQueNaoPontua(),
            EtapaInput("Objetiva I", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, primeira.Id),
            EtapaInput("Objetiva II", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 5m, 2, segunda.Id));

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
        EtapaProcesso etapa = EtapaDoProcesso("Prova Objetiva", CaraterEtapa.Classificatoria, 1, ProvaObjetivaCongelada(true, snapshotAdmiteEliminacao), 1m);
        ProcessoSeletivo processo = ProcessoComAsEtapas(etapa);

        Result result = await ExecutarSobre(
            processo, ReaderSemTipoAtivo(),
            EtapaInput("Prova Objetiva", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 1m, 5m, 1, etapa.Id));

        ConferirCaraterAceitoOuRecusado(processo, result, aceito);
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
        ITipoEtapaReader tipoEtapaReader = ReaderComTipoQueNaoPontua();

        // Edita só o peso; o caráter classificatório continua o mesmo que o tipo passou a recusar.
        Result result = await ExecutarSobre(
            processo, tipoEtapaReader, EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 7m, null, 1, etapaOriginal.Id));

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
        Guid tipoInativo = Guid.CreateVersion7();
        ITipoEtapaReader reader = ReaderComTipoQueNaoPontua();
        reader.ObterAtivoPorIdAsync(tipoInativo, Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);

        Result result = await ExecutarSobre(
            ProcessoSemEtapas(), reader,
            EtapaInput("Análise", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
            EtapaInput("Entrevista", CaraterEtapa.Eliminatoria, tipoInativo, null, 5m, 2));

        result.Errors.Select(erro => erro.Field).Should().Equal("etapas[0].carater", "etapas[1].tipoEtapaOrigemId");
    }

    [Fact(DisplayName = "Handle acumula a recusa de caráter das duas etapas do payload")]
    public async Task Handle_DuasEtapasComCaraterNaoAdmitido_AcumulaAsDuas()
    {
        Result result = await ExecutarSobre(
            ProcessoSemEtapas(), ReaderComTipoQueNaoPontua(),
            EtapaInput("Análise 1", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
            EtapaInput("Análise 2", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 2m, 5m, 2));

        result.Errors.Select(erro => erro.Field).Should().Equal("etapas[0].carater", "etapas[1].carater");
    }

    private static void ConferirCaraterAceitoOuRecusado(ProcessoSeletivo processo, Result result, bool aceito)
    {
        if (aceito)
        {
            result.IsSuccess.Should().BeTrue(result.Error?.Message);
            processo.Etapas.Single().Carater.Should().Be(CaraterEtapa.Ambas);
            return;
        }

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].carater");
        result.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.CaraterNaoAdmitidoPeloTipo);
    }
}

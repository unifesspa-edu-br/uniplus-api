namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DeclaracaoDasBancasDaEtapaTests
{
    private static readonly Guid TipoDaProva = Guid.CreateVersion7();
    private static readonly TipoBancaView TipoDeEntrevista = new(Guid.CreateVersion7(), TipoDeBanca.Entrevista, "Banca de entrevista", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.NotaDaEtapa]);
    private static readonly TipoBancaView TipoDeRecursos = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursos, "Banca de análise de recursos", null, null, PapelDaBanca.JulgaAPrimeiraInstancia, false, false, []);
    private static readonly TipoBancaView TipoDeSegundaInstancia = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância", null, null, PapelDaBanca.JulgaASegundaInstancia, false, false, []);
    private static readonly string[] CamposDaPrimeiraEDaTerceiraBanca =
    [
        "bancas[0].regraDeDeliberacao.integrantes",
        "bancas[0].regraDeDeliberacao.criterio",
        "bancas[0].regraDeDeliberacao.encerramento",
        "bancas[2].regraDeDeliberacao.integrantes",
        "bancas[2].regraDeDeliberacao.criterio",
        "bancas[2].regraDeDeliberacao.encerramento",
    ];

    [Fact(DisplayName = "A banca da etapa copia do cadastro o tipo, o nome, o papel, as marcas e os tipos de decisão julgados, com a regra declarada")]
    public async Task Declarar_ComRegra_CongelaOTipoEARegra()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.Valida())), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Should().ContainSingle().Subject;
        banca.Tipo.Should().Be(new TipoDeBancaCopiado(
            TipoDeEntrevista.Id, TipoDeBanca.Entrevista, "Banca de entrevista", PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.NotaDaEtapa]));
        banca.RegraDeDeliberacao!.Integrantes.Should().Be(3);
    }

    [Fact(DisplayName = "A banca devolvida com o id dela continua a mesma, e a posição dentro do tipo segue a ordem declarada")]
    public async Task Declarar_ComOIdDaBanca_MantemABancaEPosicionaPelaOrdemDeclarada()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null), Banca(TipoDeEntrevista, null)), CancellationToken.None);
        Guid[] ids = [.. etapa.Bancas.Select(static b => b.Id)];

        Result resultado = await Declaracao().DeclararAsync(
            etapa, Entrada(Banca(TipoDeEntrevista, null, ids[1]), Banca(TipoDeEntrevista, null, ids[0])), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Select(static b => (b.Id, b.PosicaoNoTipo)).Should().BeEquivalentTo([(ids[1], 1), (ids[0], 2)]);
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo na mesma etapa são aceitas, cada uma com a própria identidade e posição")]
    public async Task Declarar_DuasBancasDoMesmoTipo_SaoAceitas()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null), Banca(TipoDeEntrevista, null)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Select(static b => b.PosicaoNoTipo).Should().BeEquivalentTo([1, 2]);
        etapa.Bancas.Select(static b => b.Id).Distinct().Should().HaveCount(2);
    }

    [Fact(DisplayName = "A banca devolvida sem o id dela é criada de novo, com id novo")]
    public async Task Declarar_SemOIdDaBanca_CriaABancaDeNovo()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);
        Guid idAnterior = etapa.Bancas.Single().Id;

        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);

        etapa.Bancas.Single().Id.Should().NotBe(idAnterior);
    }

    [Fact(DisplayName = "Duas bancas com o mesmo id de banca gravada: a segunda é recusada em bancas[p].id, sem alterar a etapa")]
    public async Task Declarar_ComOMesmoIdEmDuasBancas_RecusaASegunda()
    {
        EtapaProcesso etapa = NovaEtapa();
        Guid idRepetido = Guid.CreateVersion7();

        Result resultado = await Declaracao().DeclararAsync(
            etapa, Entrada(Banca(TipoDeEntrevista, null, idRepetido), Banca(TipoDeRecursos, null, idRepetido)), CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            erro => erro.Field == "bancas[1].id" && erro.Error.Code == "Banca.IdDuplicado");
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A recusa do id repetido sai junto com as violações da regra das bancas")]
    public async Task Declarar_ComIdRepetidoERegraInvalida_AcumulaAsRecusas()
    {
        EtapaProcesso etapa = NovaEtapa();
        Guid idRepetido = Guid.CreateVersion7();

        Result resultado = await Declaracao().DeclararAsync(
            etapa,
            Entrada(Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.ComTresViolacoes(), idRepetido), Banca(TipoDeRecursos, null, idRepetido)),
            CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().Contain("bancas[0].regraDeDeliberacao.integrantes").And.Contain("bancas[1].id");
    }

    [Fact(DisplayName = "A banca sem regra é rascunho e a declaração a aceita")]
    public async Task Declarar_SemRegra_AceitaORascunho()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Single().RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "As violações da regra de todas as bancas saem juntas, cada uma sob o caminho da sua banca")]
    public async Task Declarar_ComRegrasInvalidas_AcumulaAsViolacoesDeTodasAsBancas()
    {
        EtapaProcesso etapa = NovaEtapa();
        EtapaProcessoInput entrada = Entrada(
            Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.ComTresViolacoes()),
            Banca(TipoDeRecursos, RegrasDeDeliberacaoDeTeste.Valida()),
            Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.ComTresViolacoes()));

        Result resultado = await Declaracao().DeclararAsync(etapa, entrada, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(CamposDaPrimeiraEDaTerceiraBanca);
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo de banca ausente do cadastro é recusado no campo da banca, sem alterar a etapa")]
    public async Task Declarar_ComTipoAusenteDoCadastro_RecusaNoCampoDaBancaSemAlterarAEtapa()
    {
        EtapaProcesso etapa = NovaEtapa();
        BancaDaEtapaInput desconhecida = new(Guid.CreateVersion7(), RegrasDeDeliberacaoDeTeste.Valida());

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(desconhecida), CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("bancas[0].tipoBancaId");
        recusa.Error.Code.Should().Be(EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo);
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo ausente do cadastro não esconde a regra inválida de outra banca: as duas recusas saem juntas")]
    public async Task Declarar_ComTipoAusenteERegraInvalida_AcumulaAsDuasRecusas()
    {
        EtapaProcesso etapa = NovaEtapa();
        BancaDaEtapaInput desconhecida = new(Guid.CreateVersion7(), RegrasDeDeliberacaoDeTeste.Valida());

        Result resultado = await Declaracao().DeclararAsync(
            etapa, Entrada(desconhecida, Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.ComTresViolacoes())), CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
            "bancas[0].tipoBancaId", "bancas[1].regraDeDeliberacao.integrantes", "bancas[1].regraDeDeliberacao.criterio", "bancas[1].regraDeDeliberacao.encerramento");
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Redeclarar a banca com o id lido reaproveita a linha e atualiza a regra")]
    public async Task Declarar_ReaproveitandoABanca_AtualizaARegraSemTrocarALinha()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);
        Guid linha = etapa.Bancas.Single().Id;

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.Valida(), linha)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Should().ContainSingle().Subject;
        banca.Id.Should().Be(linha);
        banca.RegraDeDeliberacao!.Quorum.Should().Be(2);
    }

    [Fact(DisplayName = "A banca da etapa em ato individual sem votação, ou com a votação do recurso inteira, é declarada")]
    public async Task Declarar_AtoIndividualSemVotacaoOuComVotacaoInteira_Aceita()
    {
        EtapaProcesso etapa = NovaEtapa();
        EtapaProcessoInput entrada = Entrada(
            Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.AtoIndividual()),
            Banca(TipoDeRecursos, RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso()));

        Result resultado = await Declaracao().DeclararAsync(etapa, entrada, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Select(static banca => banca.RegraDeDeliberacao!.QuorumExigido).Should().BeEquivalentTo([1, 2]);
    }

    [Fact(DisplayName = "A banca da etapa com a votação do recurso pela metade recusa o campo que falta, no caminho da banca")]
    public async Task Declarar_AtoIndividualComVotacaoPelaMetade_RecusaOsCamposQueFaltam()
    {
        EtapaProcesso etapa = NovaEtapa();
        EtapaProcessoInput entrada = Entrada(
            Banca(TipoDeEntrevista, RegrasDeDeliberacaoDeTeste.AtoIndividual()),
            Banca(TipoDeRecursos, RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum()));

        Result resultado = await Declaracao().DeclararAsync(etapa, entrada, CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "bancas[1].regraDeDeliberacao.criterio",
            "bancas[1].regraDeDeliberacao.encerramento",
        ]);
        resultado.Errors.Should().OnlyContain(static erro => erro.Error.Code == "Banca.VotacaoDoRecursoIncompleta");
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca nova do tipo de 2ª instância copia do cadastro o tipo e o papel que julga o recurso em 2ª instância")]
    public async Task Declarar_BancaNovaDeSegundaInstancia_CopiaOPapel()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeSegundaInstancia, RegrasDeDeliberacaoDeTeste.Valida())), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Single().Tipo.Should().Be(new TipoDeBancaCopiado(
            TipoDeSegundaInstancia.Id, TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância",
            PapelDaBanca.JulgaASegundaInstancia, false, false, []));
    }

    [Fact(DisplayName = "A banca de 2ª instância da etapa com a votação do recurso pela metade recusa o campo que falta, no caminho da banca")]
    public async Task Declarar_SegundaInstanciaComVotacaoPelaMetade_RecusaOsCamposQueFaltam()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(
            etapa, Entrada(Banca(TipoDeSegundaInstancia, RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum())), CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
            ["bancas[0].regraDeDeliberacao.criterio", "bancas[0].regraDeDeliberacao.encerramento"]);
        resultado.Errors.Should().OnlyContain(static erro => erro.Error.Code == "Banca.VotacaoDoRecursoIncompleta");
    }

    [Fact(DisplayName = "A banca que continua com o mesmo tipo mantém a cópia, ainda que o tipo tenha saído do cadastro")]
    public async Task Declarar_MesmoTipoQueSaiuDoCadastro_MantemACopia()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);
        BancaDaEtapa gravada = etapa.Bancas.Single();
        TipoDeBancaCopiado copia = gravada.Tipo;

        Result resultado = await DeclaracaoSemOCadastro().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null, gravada.Id)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Single().Tipo.Should().BeSameAs(copia);
    }

    [Fact(DisplayName = "A banca que troca de tipo copia o cadastro atual do tipo novo")]
    public async Task Declarar_TrocaDeTipo_CopiaOCadastroAtual()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);
        Guid linha = etapa.Bancas.Single().Id;

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoDeRecursos, null, linha)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Single();
        banca.Id.Should().Be(linha);
        banca.Tipo.OrigemId.Should().Be(TipoDeRecursos.Id);
        banca.Tipo.Papel.Should().Be(PapelDaBanca.JulgaAPrimeiraInstancia);
    }

    [Fact(DisplayName = "A banca nova com tipo desativado interrompe a declaração sem citar o id recebido")]
    public async Task Declarar_BancaNovaComTipoDesativado_InterrompeSemCitarOId()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await DeclaracaoSemOCadastro().DeclararAsync(etapa, Entrada(Banca(TipoDeEntrevista, null)), CancellationToken.None);

        resultado.Error!.Message.Should().Be("O tipo de banca indicado não existe ou está desativado no cadastro.");
    }

    private static DeclaracaoDasBancasDaEtapa DeclaracaoSemOCadastro() => new(new LeituraMemoizada<Guid, TipoBancaView>(
        (_, _) => Task.FromResult<TipoBancaView?>(null)));

    private static DeclaracaoDasBancasDaEtapa Declaracao() => new(new LeituraMemoizada<Guid, TipoBancaView>(
        (tipoId, _) => Task.FromResult<TipoBancaView?>(new[] { TipoDeEntrevista, TipoDeRecursos, TipoDeSegundaInstancia }.FirstOrDefault(tipo => tipo.Id == tipoId))));

    private static BancaDaEtapaInput Banca(TipoBancaView tipo, RegraDeDeliberacaoInput? regra, Guid? id = null) => new(tipo.Id, regra, id);

    private static EtapaProcessoInput Entrada(params BancaDaEtapaInput[] bancas) => new(
        "Prova oral", CaraterEtapa.Classificatoria, TipoDaProva, 1m, null, 1, Produtos: [], Bancas: bancas, Recursos: []);

    private static EtapaProcesso NovaEtapa() => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: "Prova oral", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(TipoDaProva, "PROVA_ORAL", "Prova oral", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}

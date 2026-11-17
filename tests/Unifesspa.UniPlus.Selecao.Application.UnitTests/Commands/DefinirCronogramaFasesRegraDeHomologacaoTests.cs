namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A regra de homologação das inscrições que a fase declara no cronograma: a montagem confere cada condição pelo
/// domínio e o enquadramento contra o vocabulário de fatos, acumula as recusas por posição, cada uma no campo da regra
/// que a causou (ADR-0125), e a regra ausente remove a que a fase tinha.
/// </summary>
public sealed class DefinirCronogramaFasesRegraDeHomologacaoTests
{
    private const string CampoDaRegra = "fases[0].regraDeHomologacao";
    private const string TipoDoRetornoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";
    private const string FatoDoVocabulario = "MAIOR_IDADE";

    private static CondicaoDeHomologacaoInput Condicao(
        string? tipo = TipoDoRetornoDoInep, int? edicao = 2026, string? motivo = "SEM_RESULTADO_INEP",
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? enquadramento = null) => new(tipo, edicao, motivo, enquadramento);

    private static AlternativaDeCondicoesInput Alternativa(params CondicaoDeHomologacaoInput[] condicoes) => new(condicoes);

    private static RegraDeHomologacaoInput Regra(string? resultado = AtoDaClassificacao, params AlternativaDeCondicoesInput[] alternativas) =>
        new(resultado, alternativas);

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Enquadramento(string fato, bool valor = true) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    private static FatoCandidatoView Fato(string codigo) => new(
        Guid.CreateVersion7(), codigo, "Maior de idade", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO",
        $"CAMPO_FORMULARIO:{codigo}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static CenarioDoCronogramaDeFases Cenario() =>
        CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao()).ComFatosNoCatalogo(Fato(FatoDoVocabulario));

    private static FaseCronogramaInput Fase(CenarioDoCronogramaDeFases cenario, RegraDeHomologacaoInput? regra) =>
        cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { RegraDeHomologacao = regra };

    private static FaseCronogramaInput FaseComRecursoRecusado(CenarioDoCronogramaDeFases cenario, RegraDeHomologacaoInput regra) =>
        Fase(cenario, regra) with { RegraRecurso = RecursoAncoradoNaClassificacao() with { RegraCodigo = RegraDeBonus().Codigo } };

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static RegraDeHomologacaoDto? RegraGravada(CenarioDoCronogramaDeFases cenario) =>
        ProjecaoDaFaseDoCronograma.ProjetarFase(cenario.Processo.CronogramaFases.Single()).Publicacoes.RegraDeHomologacao;

    private static RegraDeHomologacaoInput RegraComDuasAlternativas() => Regra(
        AtoDaClassificacao,
        Alternativa(Condicao(edicao: 2025, motivo: "MOTIVO_B"), Condicao(edicao: 2024, motivo: "MOTIVO_A", enquadramento: Enquadramento(FatoDoVocabulario))),
        Alternativa(Condicao(edicao: 2023, motivo: null)));

    [Fact(DisplayName = "A regra declarada é gravada na fase e devolvida na leitura, com as alternativas e os motivos na ordem declarada")]
    public async Task Handle_RegraDeclarada_GravaEDevolveNaOrdemDeclarada()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, RegraComDuasAlternativas()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        RegraDeHomologacaoDto lida = RegraGravada(cenario)!;
        lida.ResultadoAtoCodigo.Should().Be(AtoDaClassificacao);
        lida.Alternativas.Select(static a => string.Join(',', a.Condicoes.Select(static c => c.EdicaoDoExame))).Should().Equal("2025,2024", "2023");
        lida.Alternativas[0].Condicoes.Select(static c => c.MotivoDesfavoravelCodigo).Should().Equal("MOTIVO_B", "MOTIVO_A");
    }

    [Fact(DisplayName = "A leitura devolve o tipo da condição e o enquadramento no formato que a escrita recebe")]
    public async Task Handle_RegraDeclarada_DevolveTipoEEnquadramentoNaFormaDaEscrita()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        await cenario.DefinirFasesAsync(Fase(cenario, RegraComDuasAlternativas()));

        CondicaoDeHomologacaoDto semEnquadramento = RegraGravada(cenario)!.Alternativas[0].Condicoes[0];
        CondicaoPrecondicaoDto enquadramento = RegraGravada(cenario)!.Alternativas[0].Condicoes[1].Enquadramento!.Single().Single();
        semEnquadramento.Tipo.Should().Be(TipoDoRetornoDoInep);
        semEnquadramento.Enquadramento.Should().BeNull();
        (enquadramento.Fato, enquadramento.Operador).Should().Be((FatoDoVocabulario, "IGUAL"));
    }

    [Fact(DisplayName = "A regra sem alternativa é aceita e devolvida vazia: a fase homologa toda inscrição enviada")]
    public async Task Handle_RegraVazia_GravaSemAlternativas()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        RegraGravada(cenario)!.Alternativas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que não devolve a regra a perde: a regra ausente remove a que a fase tinha")]
    public async Task Handle_RegraAusente_RemoveARegraQueAFaseTinha()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        (await cenario.DefinirFasesAsync(Fase(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao()))))).IsSuccess.Should().BeTrue();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra: null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().RegraDeHomologacao.Should().BeNull();
        RegraGravada(cenario).Should().BeNull();
    }

    [Fact(DisplayName = "Tipo, edição e motivo recusados na mesma condição saem juntos, cada um no seu campo")]
    public async Task Handle_CondicaoComVariasRecusas_AcumulaPorCampo()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(AtoDaClassificacao, Alternativa(Condicao(tipo: "TAXA_PAGA", edicao: 1997, motivo: "motivo ruim")));

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel),
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida),
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].motivoDesfavoravelCodigo", MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido));
    }

    [Fact(DisplayName = "As recusas de condições e alternativas diferentes saem por posição, cada uma com os índices da sua")]
    public async Task Handle_RecusasEmVariasPosicoes_PrefixamOsIndices()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(
            AtoDaClassificacao,
            Alternativa(Condicao(), Condicao(edicao: 2101)),
            Alternativa(Condicao(), Condicao(), Condicao(tipo: null)));

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[1].edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida),
            ($"{CampoDaRegra}.alternativas[1].condicoes[2].tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel));
    }

    [Theory(DisplayName = "A alternativa sem condição, ausente ou vazia, é recusada no campo das condições dela")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AlternativaSemCondicao_RecusaNoCampoDasCondicoes(bool condicoesAusentes)
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        AlternativaDeCondicoesInput semCondicao = condicoesAusentes ? new(null) : new([]);

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao()), semCondicao)));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[1].condicoes", HomologacaoDasInscricoesErrorCodes.AlternativaSemCondicao));
    }

    [Fact(DisplayName = "Enquadramento com fato fora do vocabulário é recusado no campo do enquadramento da condição")]
    public async Task Handle_EnquadramentoComFatoForaDoVocabulario_RecusaNoEnquadramento()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(AtoDaClassificacao, Alternativa(Condicao(enquadramento: Enquadramento("FATO_QUE_NAO_EXISTE"))));

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra));

        Recusas(resultado).Should().Equal(($"{CampoDaRegra}.alternativas[0].condicoes[0].enquadramento[0][0].fato", "PredicadoDnf.FatoDesconhecido"));
    }

    [Fact(DisplayName = "Enquadramento mal formado é recusado no campo do enquadramento sem ler o vocabulário")]
    public async Task Handle_EnquadramentoComClausulaVazia_RecusaNoEnquadramento()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(AtoDaClassificacao, Alternativa(Condicao(enquadramento: [[]])));

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra));

        Recusas(resultado).Should().Equal(($"{CampoDaRegra}.alternativas[0].condicoes[0].enquadramento[0]", "ClausulaDnf.ClausulaVazia"));
        await cenario.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "A recusa do enquadramento não impede o julgamento do tipo da mesma condição")]
    public async Task Handle_EnquadramentoETipoRecusados_AcumulaOsDois()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(
            AtoDaClassificacao, Alternativa(Condicao(tipo: "TAXA_PAGA", enquadramento: Enquadramento("FATO_QUE_NAO_EXISTE"))));

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, regra));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel),
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].enquadramento[0][0].fato", "PredicadoDnf.FatoDesconhecido"));
    }

    [Fact(DisplayName = "O vocabulário de fatos é lido uma vez por edição, qualquer que seja o número de enquadramentos")]
    public async Task Handle_VariosEnquadramentosEmVariasFases_LeOVocabularioUmaVez()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        RegraDeHomologacaoInput regra = Regra(
            AtoDaClassificacao, Alternativa(Condicao(enquadramento: Enquadramento(FatoDoVocabulario)), Condicao(enquadramento: Enquadramento(FatoDoVocabulario, valor: false))));
        FaseCronogramaInput primeira = Fase(cenario, regra);
        FaseCronogramaInput segunda = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Ordem = 2, RegraDeHomologacao = regra };

        Result resultado = await cenario.DefinirFasesAsync(primeira, segunda);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await cenario.FatoCandidatoReader.Received(1).ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regra sem enquadramento nenhum não lê o vocabulário de fatos")]
    public async Task Handle_RegraSemEnquadramento_NaoLeOVocabulario()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao()))));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await cenario.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "Resultado apontado que não é produto da fase é recusado no campo do resultado")]
    public async Task Handle_ResultadoQueNaoEProdutoDaFase_RecusaNoResultado()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra("OUTRO_RESULTADO", Alternativa(Condicao()))));

        Recusas(resultado).Should().Equal(($"{CampoDaRegra}.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
        cenario.Processo.CronogramaFases.Should().BeEmpty();
    }

    [Fact(DisplayName = "Regra sem resultado apontado é recusada como resultado não declarado")]
    public async Task Handle_RegraSemResultado_RecusaNoResultado()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra(resultado: null, Alternativa(Condicao()))));

        Recusas(resultado).Should().Equal(($"{CampoDaRegra}.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "A recusa da condição acumula com a do resultado apontado que não é produto da fase")]
    public async Task Handle_CondicaoRecusadaEResultadoForaDaFase_AcumulaAsDuas()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra("OUTRO_RESULTADO", Alternativa(Condicao(edicao: 1997)))));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida),
            ($"{CampoDaRegra}.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "A recusa da condição acumula com a falta do resultado da regra")]
    public async Task Handle_CondicaoRecusadaESemResultado_AcumulaAsDuas()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra(resultado: null, Alternativa(Condicao(tipo: "TAXA_PAGA")))));

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel),
            ($"{CampoDaRegra}.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    private static FaseCronogramaInput FaseComProdutoDePapelDesconhecido(CenarioDoCronogramaDeFases cenario, RegraDeHomologacaoInput regra) =>
        Fase(cenario, regra) with { Produtos = [new ProdutoDaFaseInput(AtoDaClassificacao, "PAPEL_QUE_NAO_EXISTE")] };

    [Fact(DisplayName = "O produto recusado não esconde a falta do resultado da regra: a falta não depende dos produtos")]
    public async Task Handle_ProdutoRecusadoESemResultado_AcumulaAsDuas()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(FaseComProdutoDePapelDesconhecido(cenario, Regra(resultado: null)));

        Recusas(resultado).Should().Equal(
            ("fases[0].produtos[0].papel", "ProdutoDaFase.PapelDesconhecido"),
            ($"{CampoDaRegra}.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "O produto recusado deixa sem julgar o resultado apontado, que pode ser justamente ele")]
    public async Task Handle_ProdutoRecusadoEResultadoApontado_NaoJulgaOResultado()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(FaseComProdutoDePapelDesconhecido(cenario, Regra("PRODUTO_QUE_NAO_EXISTE")));

        Recusas(resultado).Select(static r => r.Campo).Should().Equal("fases[0].produtos[0].papel");
    }

    [Fact(DisplayName = "O produto recusado acumula com a recusa da condição, antes da falta do resultado")]
    public async Task Handle_ProdutoCondicaoEResultado_AcumulaNaOrdemDaDeclaracao()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(
            FaseComProdutoDePapelDesconhecido(cenario, Regra(resultado: null, Alternativa(Condicao(edicao: 1997)))));

        Recusas(resultado).Select(static r => r.Campo).Should().Equal(
            "fases[0].produtos[0].papel",
            $"{CampoDaRegra}.alternativas[0].condicoes[0].edicaoDoExame",
            $"{CampoDaRegra}.resultadoAtoCodigo");
    }

    [Fact(DisplayName = "A regra de recurso recusada não impede a conferência da regra de homologação da mesma fase")]
    public async Task Handle_RegraDeRecursoRecusada_AcumulaComARegraDeHomologacao()
    {
        CenarioDoCronogramaDeFases cenario = Cenario().ComRegraNoCatalogo(RegraDeBonus());

        Result resultado = await cenario.DefinirFasesAsync(
            FaseComRecursoRecusado(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao(edicao: 1997)))));

        Recusas(resultado).Should().Equal(
            ("fases[0].regraRecurso.regraCodigo", "RegraRecursoFase.RegraCatalogoInvalida"),
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida));
    }

    [Fact(DisplayName = "A regra de recurso recusada em uma fase não impede a conferência das demais fases")]
    public async Task Handle_RegraDeRecursoRecusada_AcumulaComAsDemaisFases()
    {
        CenarioDoCronogramaDeFases cenario = Cenario().ComRegraNoCatalogo(RegraDeBonus());
        FaseCronogramaInput outraFase = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Ordem = 2, RegraDeHomologacao = Regra("OUTRO_RESULTADO") };

        Result resultado = await cenario.DefinirFasesAsync(FaseComRecursoRecusado(cenario, Regra()), outraFase);

        Recusas(resultado).Should().Equal(
            ("fases[0].regraRecurso.regraCodigo", "RegraRecursoFase.RegraCatalogoInvalida"),
            ("fases[1].regraDeHomologacao.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "A recusa da regra acumula com a da janela invertida da mesma fase e com a da regra de outra fase")]
    public async Task Handle_RegraRecusadaEOutrasRecusas_AcumulaPorFase()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();
        FaseCronogramaInput comJanelaInvertida = Fase(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao(edicao: 1997)))) with
        {
            Inicio = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        FaseCronogramaInput outraFase = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Ordem = 2, RegraDeHomologacao = Regra("OUTRO_RESULTADO") };

        Result resultado = await cenario.DefinirFasesAsync(comJanelaInvertida, outraFase);

        Recusas(resultado).Should().Equal(
            ($"{CampoDaRegra}.alternativas[0].condicoes[0].edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida),
            ("fases[0].fim", "FaseCronograma.JanelaInvertida"),
            ("fases[1].regraDeHomologacao.resultadoAtoCodigo", HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado));
    }

    [Fact(DisplayName = "A recusa da regra não muda o processo nem grava")]
    public async Task Handle_RegraRecusada_NaoMutaOProcessoNemGrava()
    {
        CenarioDoCronogramaDeFases cenario = Cenario();

        Result resultado = await cenario.DefinirFasesAsync(Fase(cenario, Regra(AtoDaClassificacao, Alternativa(Condicao(tipo: null)))));

        resultado.IsFailure.Should().BeTrue();
        cenario.Processo.CronogramaFases.Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }
}

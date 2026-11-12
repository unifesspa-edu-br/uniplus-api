namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RemocaoDeFaseIniciadaTests
{
    private const string CodigoDaRemocao = "FaseCronograma.FaseIniciadaNaoSaiDoCronograma";
    private const string CodigoDaTroca = "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda";

    [Fact(DisplayName = "Remover do cronograma uma fase iniciada é recusado, citando a fase, e o cronograma não muda")]
    public void Remover_FaseIniciada_Recusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        ProcessoSeletivo processo = ProcessoComCronograma(habilitacao, classificacao);

        Result resultado = processo.DefinirCronogramaFases([classificacao], [], PrecondicaoIfMatch.Curinga, Iniciadas(habilitacao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CodigoDaRemocao);
        resultado.Error.Message.Should().Contain("HABILITACAO");
        processo.CronogramaFases.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Remover do cronograma uma fase que ainda não começou é aceito")]
    public void Remover_FaseNaoIniciada_Aceita()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        ProcessoSeletivo processo = ProcessoComCronograma(habilitacao, classificacao);

        Result resultado = processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Curinga, Iniciadas(habilitacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("HABILITACAO");
    }

    [Fact(DisplayName = "Redefinir o cronograma mantendo a fase iniciada com a mesma unidade não é remoção")]
    public void Manter_FaseIniciadaComAMesmaUnidade_Aceita()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        ProcessoSeletivo processo = ProcessoComCronograma(habilitacao, classificacao);

        Result resultado = processo.DefinirCronogramaFases(
            [classificacao, habilitacao], [], PrecondicaoIfMatch.Curinga, Iniciadas(habilitacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Recolocar a fase iniciada com outra unidade cai na recusa da troca, e não na da remoção")]
    public void Recolocar_FaseIniciadaComOutraUnidade_RecusaATroca()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        ProcessoSeletivo processo = ProcessoComCronograma(habilitacao, classificacao);

        Result resultado = processo.DefinirCronogramaFases(
            [classificacao, ComUnidade(habilitacao, UnidadesDeTeste.Cga)], [], PrecondicaoIfMatch.Curinga, Iniciadas(habilitacao));

        resultado.Error!.Code.Should().Be(CodigoDaTroca);
    }

    [Fact(DisplayName = "A fase iniciada que a versão vigente já não traz não é removida pela retificação")]
    public void Remover_FaseIniciadaForaDaVersaoVigente_NaoRecusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(habilitacao.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId)]);

        iniciadas.RecusaDaRemocao([classificacao]).Should().BeNull();
    }

    [Fact(DisplayName = "O fechamento confere a remoção sobre o cronograma do rascunho")]
    public void Fechamento_FaseIniciadaForaDoRascunho_Recusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        FasesIniciadas iniciadas = Iniciadas(habilitacao);

        iniciadas.RecusaDaAlteracao([classificacao])!.Code.Should().Be(CodigoDaRemocao);
        iniciadas.RecusaDaAlteracao([classificacao, habilitacao]).Should().BeNull();
        FasesIniciadas.Nenhuma.RecusaDaAlteracao([classificacao]).Should().BeNull();
    }

    private static FasesIniciadas Iniciadas(FaseCronograma fase) => new(
        [KeyValuePair.Create<Guid, Guid?>(fase.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId)],
        [KeyValuePair.Create(fase.FaseCanonicaOrigemId, fase.Codigo)]);

    private static (FaseCronograma Habilitacao, FaseCronograma Classificacao) DuasFases() =>
        (Fase(1, "HABILITACAO"), Fase(2, "CLASSIFICACAO"));

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), produtos: [],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static ProcessoSeletivo ProcessoComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Remocao de fase", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma ComUnidade(FaseCronograma atual, UnidadeResponsavelSnapshot unidade) =>
        FaseCronograma.Criar(
            atual.Ordem, atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData,
            atual.AgrupaEtapas, atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao,
            atual.Inicio, atual.Fim, [.. atual.Produtos], atual.FaseConcluinteCodigo, atual.EmiteParecerIndividual,
            [.. atual.BancasRequeridas], atual.RegraRecurso, unidade).Value!;
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O ponto de extensão da cobertura dos motivos e a exigência da isenção da taxa isolada.</summary>
public sealed class ExigenciasDeMotivosTests
{
    [Fact(DisplayName = "O registro das exigências começa pela da isenção da taxa")]
    public void Registro_ComecaPelaIsencao()
    {
        ExigenciasDeMotivos.Todas.Select(static exigencia => exigencia.GetType()).Should().Equal(
            typeof(ExigenciaDaIsencaoDaTaxa), typeof(ExigenciaDaHomologacaoDasInscricoes));
    }

    [Fact(DisplayName = "A cobertura agrega as faltas de cada exigência na ordem do registro")]
    public void Cobertura_AgregaFaltasDeVariasExigencias()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");
        IExigenciaDeMotivos[] exigencias = [ExigenciaFalsa.DeCobertura("primeira"), new ExigenciaDaIsencaoDaTaxa(), ExigenciaFalsa.DeCobertura("terceira")];

        FieldError[] faltas = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [], exigencias)];

        faltas.Select(e => e.Error.Code).Should().Equal(
            "primeira", MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, "terceira");
    }

    [Fact(DisplayName = "A exigência de teste isolada recusa mesmo sem taxa e sem fundamento")]
    public void Cobertura_ExigenciaAdicionalRecusaSozinha()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [], [ExigenciaFalsa.DeCobertura("outra")])
            .Should().ContainSingle().Which.Error.Code.Should().Be("outra");
    }

    [Fact(DisplayName = "A cobertura pública percorre o registro das exigências")]
    public void CoberturaPublica_UsaORegistro()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");

        FieldError[] publica = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [])];

        publica.Should().HaveCount(2);
        publica.Select(e => e.Error.Code).Should().OnlyContain(c => c == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "Sem taxa, a isenção não tem falta")]
    public void Isencao_SemTaxa_SemFaltas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        new ExigenciaDaIsencaoDaTaxa().FaltasDeCobertura(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), []).Should().BeEmpty();
    }

    [Fact(DisplayName = "A isenção cobra os dois sentidos de cada fundamento e cita a fase, o fundamento e o sentido que faltam")]
    public void Isencao_CitaOFundamentoEOSentido()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO", "DOACAO_MEDULA_OSSEA");
        MotivoDeDecisaoDoProcesso[] escolhidos = [Escolher(processo, "CADASTRO_UNICO", SentidoDoMotivo.Favoravel)];

        FieldError[] faltas = [.. new ExigenciaDaIsencaoDaTaxa().FaltasDeCobertura(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), escolhidos)];

        faltas.Should().HaveCount(3).And.OnlyContain(f => f.Field == "motivosDeDecisao");
        faltas.Select(f => f.Error.Message).Should().Equal(
        [
            $"A fase 'INSCRICAO' precisa de ao menos um motivo desfavorável para o fundamento de isenção {"CADASTRO_UNICO"}.",
            $"A fase 'INSCRICAO' precisa de ao menos um motivo favorável para o fundamento de isenção {"DOACAO_MEDULA_OSSEA"}.",
            $"A fase 'INSCRICAO' precisa de ao menos um motivo desfavorável para o fundamento de isenção {"DOACAO_MEDULA_OSSEA"}.",
        ]);
    }

    [Fact(DisplayName = "Com os dois sentidos escolhidos para o fundamento, a isenção não tem falta")]
    public void Isencao_Coberta_SemFaltas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");
        MotivoDeDecisaoDoProcesso[] escolhidos =
        [
            Escolher(processo, "CADASTRO_UNICO", SentidoDoMotivo.Favoravel),
            Escolher(processo, "CADASTRO_UNICO", SentidoDoMotivo.Desfavoravel),
        ];

        new ExigenciaDaIsencaoDaTaxa().FaltasDeCobertura(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), escolhidos).Should().BeEmpty();
    }

    [Fact(DisplayName = "A cobertura emite primeiro os tipos não declarados de todas as exigências e depois as faltas de cobertura de todas")]
    public void Cobertura_TiposNaoDeclaradosAntecedemACoberturaDeTodasAsExigencias()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        IExigenciaDeMotivos[] exigencias = [ExigenciaFalsa.Com("tipoA", "coberturaA"), ExigenciaFalsa.Com("tipoB", "coberturaB")];

        FieldError[] faltas = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [], exigencias)];

        faltas.Select(e => e.Error.Code).Should().Equal("tipoA", "tipoB", "coberturaA", "coberturaB");
    }

    [Fact(DisplayName = "O tipo de homologação não declarado antecede a cobertura da isenção, mesmo com a isenção registrada primeiro")]
    public void Cobertura_TipoDaHomologacaoNaoDeclaradoAntecedeACoberturaDaIsencao()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");
        ProcessoParaMotivos.HomologandoSemDeclararOTipo(processo);

        FieldError[] faltas = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [])];

        faltas.Select(e => e.Error.Code).Should().Equal(
            MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    private static MotivoDeDecisaoDoProcesso Escolher(ProcessoSeletivo processo, string fundamento, SentidoDoMotivo sentido) =>
        ProcessoParaMotivos.Escolha(
            processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), ProcessoParaMotivos.DaIsencao(fundamento, sentido), versaoDeEntrada: 1);

    private sealed class ExigenciaFalsa(string? codigoDoTipoNaoDeclarado, string? codigoDaCobertura) : IExigenciaDeMotivos
    {
        public static ExigenciaFalsa DeCobertura(string codigo) => new(null, codigo);

        public static ExigenciaFalsa Com(string codigoDoTipoNaoDeclarado, string codigoDaCobertura) => new(codigoDoTipoNaoDeclarado, codigoDaCobertura);

        public IEnumerable<FieldError> TiposNaoDeclarados(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares) => Falta(codigoDoTipoNaoDeclarado);

        public IEnumerable<FieldError> FaltasDeCobertura(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDeDecisaoDoProcesso> escolhidos) =>
            Falta(codigoDaCobertura);

        private static IEnumerable<FieldError> Falta(string? codigo) =>
            codigo is null ? [] : [new FieldError("motivosDeDecisao", new DomainError(codigo, "Falta de teste."))];
    }
}

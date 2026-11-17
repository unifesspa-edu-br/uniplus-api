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
        ExigenciasDeMotivos.Todas.Should().ContainSingle().Which.Should().BeOfType<ExigenciaDaIsencaoDaTaxa>();
    }

    [Fact(DisplayName = "A cobertura agrega as faltas de cada exigência na ordem do registro")]
    public void Cobertura_AgregaFaltasDeVariasExigencias()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        IExigenciaDeMotivos[] exigencias = [new ExigenciaFalsa("primeira"), new ExigenciaDaIsencaoDaTaxa(), new ExigenciaFalsa("terceira")];

        FieldError[] faltas = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [], exigencias)];

        faltas.Select(e => e.Error.Code).Should().Equal(
            "primeira", MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, "terceira");
    }

    [Fact(DisplayName = "A exigência de teste isolada recusa mesmo sem taxa e sem fundamento")]
    public void Cobertura_ExigenciaAdicionalRecusaSozinha()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [], [new ExigenciaFalsa("outra")])
            .Should().ContainSingle().Which.Error.Code.Should().Be("outra");
    }

    [Fact(DisplayName = "A cobertura pública percorre o registro das exigências")]
    public void CoberturaPublica_UsaORegistro()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);

        FieldError[] publica = [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), [])];

        publica.Should().HaveCount(2);
        publica.Select(e => e.Error.Code).Should().OnlyContain(c => c == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "Sem taxa, a isenção não tem falta")]
    public void Isencao_SemTaxa_SemFaltas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        new ExigenciaDaIsencaoDaTaxa().Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), []).Should().BeEmpty();
    }

    [Fact(DisplayName = "A isenção cobra os dois sentidos de cada fundamento e cita a fase, o fundamento e o sentido que faltam")]
    public void Isencao_CitaOFundamentoEOSentido()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
        MotivoDoProcesso[] escolhidos = [Escolher(processo, FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel)];

        FieldError[] faltas = [.. new ExigenciaDaIsencaoDaTaxa().Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), escolhidos)];

        faltas.Should().HaveCount(3).And.OnlyContain(f => f.Field == "motivos");
        faltas.Select(f => f.Error.Message).Should().Equal(
        [
            $"A fase 'INSCRICAO' precisa de ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.CadastroUnico}.",
            $"A fase 'INSCRICAO' precisa de ao menos um motivo favorável para o fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea}.",
            $"A fase 'INSCRICAO' precisa de ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea}.",
        ]);
    }

    [Fact(DisplayName = "Com os dois sentidos escolhidos para o fundamento, a isenção não tem falta")]
    public void Isencao_Coberta_SemFaltas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDoProcesso[] escolhidos =
        [
            Escolher(processo, FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel),
            Escolher(processo, FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel),
        ];

        new ExigenciaDaIsencaoDaTaxa().Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), escolhidos).Should().BeEmpty();
    }

    private static MotivoDoProcesso Escolher(ProcessoSeletivo processo, string fundamento, SentidoDoMotivo sentido) =>
        ProcessoParaMotivos.Escolha(
            processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), ProcessoParaMotivos.DaIsencao(fundamento, sentido), versaoDeEntrada: 1);

    private sealed class ExigenciaFalsa(string codigo) : IExigenciaDeMotivos
    {
        public IEnumerable<FieldError> Faltas(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDoProcesso> escolhidos) =>
            [new FieldError("motivos", new DomainError(codigo, "Falta de teste."))];
    }
}

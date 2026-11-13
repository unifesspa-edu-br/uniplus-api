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

        Result resultado = CoberturaDosMotivos.Conferir(processo, [], exigencias);

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(
            "primeira", MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, "terceira");
    }

    [Fact(DisplayName = "A exigência de teste isolada recusa mesmo sem taxa e sem fundamento")]
    public void Cobertura_ExigenciaAdicionalRecusaSozinha()
    {
        Result resultado = CoberturaDosMotivos.Conferir(ProcessoParaMotivos.Rascunho(), [], [new ExigenciaFalsa("outra")]);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("outra");
    }

    [Fact(DisplayName = "Sem faltas em nenhuma exigência, a cobertura passa")]
    public void Cobertura_SemFaltas_Aceita()
    {
        CoberturaDosMotivos.Conferir(ProcessoParaMotivos.Rascunho(), [], [new ExigenciaDaIsencaoDaTaxa()]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A cobertura pública percorre o registro das exigências")]
    public void CoberturaPublica_UsaORegistro()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);

        Result publica = CoberturaDosMotivos.Conferir(processo, []);

        publica.Errors.Should().HaveCount(2);
        publica.Errors.Select(e => e.Error.Code).Should().OnlyContain(c => c == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "Sem taxa, a isenção não tem falta")]
    public void Isencao_SemTaxa_SemFaltas()
    {
        new ExigenciaDaIsencaoDaTaxa().Faltas(ProcessoParaMotivos.Rascunho(), []).Should().BeEmpty();
    }

    [Fact(DisplayName = "A isenção cobra os dois sentidos de cada fundamento e cita o fundamento e o sentido que faltam")]
    public void Isencao_CitaOFundamentoEOSentido()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
        MotivoDoProcesso[] escolhidos = [Escolher(processo, FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel)];

        FieldError[] faltas = [.. new ExigenciaDaIsencaoDaTaxa().Faltas(processo, escolhidos)];

        faltas.Should().HaveCount(3).And.OnlyContain(f => f.Field == "motivosDeDecisao");
        faltas.Select(f => f.Error.Message).Should().Equal(
        [
            $"Falta escolher ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.CadastroUnico}.",
            $"Falta escolher ao menos um motivo favorável para o fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea}.",
            $"Falta escolher ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea}.",
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

        new ExigenciaDaIsencaoDaTaxa().Faltas(processo, escolhidos).Should().BeEmpty();
    }

    [Fact(DisplayName = "Motivo de outro tipo ou de outro fundamento não cobre a isenção")]
    public void Isencao_OutroTipoOuFundamento_NaoCobre()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "DOCUMENTO_ILEGIVEL", SentidoDoMotivo.Favoravel)),
            Escolher(processo, FundamentoIsencaoCodigo.CarenciaSocioeconomica, SentidoDoMotivo.Desfavoravel),
        ];

        new ExigenciaDaIsencaoDaTaxa().Faltas(processo, escolhidos).Should().HaveCount(2);
    }

    private static MotivoDoProcesso Escolher(ProcessoSeletivo processo, string fundamento, SentidoDoMotivo sentido) =>
        Escolher(processo, ProcessoParaMotivos.DaIsencao(fundamento, sentido));

    private static MotivoDoProcesso Escolher(ProcessoSeletivo processo, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(
            processo, [], origem, null, PrecondicaoIfMatch.Ausente, ProcessoParaMotivos.InicioDaInscricao, null)).Value!;

    private sealed class ExigenciaFalsa(string codigo) : IExigenciaDeMotivos
    {
        public IEnumerable<FieldError> Faltas(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos) =>
            [new FieldError("motivosDeDecisao", new DomainError(codigo, "Falta de teste."))];
    }
}

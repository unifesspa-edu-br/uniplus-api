namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A cobertura dos motivos que as decisões do processo exigem, em cada fase ou etapa que declara o tipo (UNI-REQ-0123).</summary>
public sealed class CoberturaDosMotivosTests
{
    [Fact(DisplayName = "Processo que não cobra taxa não exige motivo de isenção")]
    public void SemTaxa_Cobertura_Aceita()
    {
        Faltas(ProcessoParaMotivos.Rascunho()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada fundamento habilitado exige um motivo favorável e um desfavorável, citando a fase e o que falta")]
    public void FundamentoSemCobertura_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
        MotivoDeDecisaoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.DoacaoMedulaOssea, SentidoDoMotivo.Favoravel, "DOACAO_CONFIRMADA")),
        ];

        FieldError falta = Faltas(processo, escolhidos).Should().ContainSingle().Subject;

        falta.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
        falta.Field.Should().Be("motivosDeDecisao");
        falta.Error.Message.Should().Be(
            $"A fase 'INSCRICAO' precisa de ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea}.");
    }

    [Fact(DisplayName = "Motivo de outro tipo ou de outro fundamento não cobre o fundamento")]
    public void MotivoDeOutroTipo_NaoCobre()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisaoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "DOCUMENTO_ILEGIVEL", SentidoDoMotivo.Favoravel)),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CarenciaSocioeconomica, SentidoDoMotivo.Desfavoravel)),
        ];

        Faltas(processo, escolhidos).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Com os dois sentidos em cada fundamento no lugar que declara o tipo, a cobertura passa")]
    public void CoberturaCompleta_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisaoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO")),
        ];

        Faltas(processo, escolhidos).Should().BeEmpty();
    }

    [Fact(DisplayName = "A cobertura é de cada lugar que declara o tipo: o motivo escolhido em outra fase não cobre esta")]
    public void Cobertura_EPorLugar()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        ProcessoParaMotivos.DeclararNaFase(processo, "HOMOLOGACAO_INSCRICOES", TipoMotivoDeDecisao.IsencaoDaTaxa);
        MotivoDeDecisaoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO")),
        ];

        FieldError[] faltas = Faltas(processo, escolhidos);

        faltas.Select(f => f.Error.Message).Should().Equal(
            $"A fase 'HOMOLOGACAO_INSCRICOES' precisa de ao menos um motivo favorável para o fundamento de isenção {FundamentoIsencaoCodigo.CadastroUnico}.",
            $"A fase 'HOMOLOGACAO_INSCRICOES' precisa de ao menos um motivo desfavorável para o fundamento de isenção {FundamentoIsencaoCodigo.CadastroUnico}.");
    }

    [Fact(DisplayName = "A etapa que declara o tipo de isenção também é lugar da cobertura, depois das fases")]
    public void Cobertura_IncluiAsEtapasQueDeclaramOTipo()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        ProcessoParaMotivos.DeclararNaEtapa(processo, "Prova discursiva", TipoMotivoDeDecisao.IsencaoDaTaxa);

        Faltas(processo).Select(f => f.Error.Message[..f.Error.Message.IndexOf(" precisa", StringComparison.Ordinal)]).Distinct().Should().Equal(
            "A fase 'INSCRICAO'", "A etapa 'Prova discursiva'");
    }

    [Fact(DisplayName = "Fundamento habilitado sem fase nem etapa que declare o tipo de isenção recusa com tipo de decisão não declarado, um por fundamento")]
    public void FundamentoSemLugarQueDeclare_RecusaComTipoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
        ProcessoParaMotivos.DeclararNaFase(processo, "INSCRICAO", TipoMotivoDeDecisao.HomologacaoDasInscricoes);

        FieldError[] faltas = Faltas(processo);

        faltas.Should().HaveCount(2).And.OnlyContain(f => f.Error.Code == MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
        faltas.Select(f => f.Error.Message).Should().Equal(
            $"O fundamento de isenção {FundamentoIsencaoCodigo.CadastroUnico} é habilitado, mas nenhuma fase nem etapa declara o tipo de decisão ISENCAO_DA_TAXA.",
            $"O fundamento de isenção {FundamentoIsencaoCodigo.DoacaoMedulaOssea} é habilitado, mas nenhuma fase nem etapa declara o tipo de decisão ISENCAO_DA_TAXA.");
    }

    [Fact(DisplayName = "Processo que não cobra taxa não exige lugar que declare o tipo de isenção")]
    public void SemTaxa_SemLugarQueDeclare_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        ProcessoParaMotivos.DeclararNaFase(processo, "INSCRICAO");

        Faltas(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Há uma política por tipo de decisão")]
    public void Politicas_UmaPorTipo() =>
        Enum.GetValues<TipoMotivoDeDecisao>().Should().OnlyContain(t => PoliticasDosTiposDeMotivo.Do(t).Tipo == t);

    [Fact(DisplayName = "Os códigos de sentido vão e voltam do wire")]
    public void Codigos_IdaEVolta()
    {
        SentidoDoMotivoCodigo.FromCodigo(SentidoDoMotivoCodigo.Favoravel).Should().Be(SentidoDoMotivo.Favoravel);
        SentidoDoMotivoCodigo.FromCodigo(null).Should().Be(SentidoDoMotivo.Nenhum);
    }

    private static FieldError[] Faltas(ProcessoSeletivo processo, params MotivoDeDecisaoDoProcesso[] escolhidos) =>
        [.. CoberturaDosMotivos.Faltas(processo.Declaracao(), ProcessoParaMotivos.Lugares(processo), escolhidos)];

    private static MotivoDeDecisaoDoProcesso Escolher(ProcessoSeletivo processo, MotivoDeDecisao origem) =>
        ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem, versaoDeEntrada: 1);
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>As fases do documento entram na ordem da posição delas, mais 1, e a que o cadastro não tem vira parte recusada que não renumera as outras.</summary>
public sealed class LeituraDasFasesTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact(DisplayName = "Duas fases declaradas entram com a fase canônica do cadastro, a unidade e a ordem pela posição mais 1")]
    public void DuasFases_EntramNaOrdemDaPosicao()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao"), Fase(1, "resultado", "RESULTADO", "CGA")));

        FaseCronograma[] fases = [.. lidos.Cronograma.CronogramaFases];
        fases.Select(static fase => (fase.Codigo, fase.Ordem)).Should().Equal(("INSCRICAO", 1), ("RESULTADO", 2));
        fases[1].UnidadeResponsavel!.Sigla.Should().Be("CGA");
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase canônica que o cadastro não tem recusa a fase inteira no campo que a cita")]
    public void FaseCanonica_Inexistente_RecusaAFase()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "digitada", "FASE-DIGITADA"), Fase(1, "resultado", "RESULTADO")));

        lidos.Cronograma.CronogramaFases.Should().ContainSingle();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/tipoFaseCodigo", FaseCronogramaErrorCodes.FaseCanonicaNaoEncontrada));
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITADA", StringComparison.Ordinal));
        Cronograma(lidos).TemParteRecusadaNaListaDeFases.Should().BeTrue();
    }

    [Fact(DisplayName = "A fase canônica inexistente não esconde as recusas independentes da mesma fase")]
    public void FaseCanonica_Inexistente_AcumulaRecusasIndependentes()
    {
        FaseNoDocumento fase = Fase(0, "fase", "FASE-DIGITADA", "UNIDADE-DIGITADA")
            .Publicando(Preliminar(3, "ATO-DIGITADO"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));

        lidos.Cronograma.CronogramaFases.Should().BeEmpty();
        lidos.Origem.PartesRecusadas.Select(static parte => parte.Causa.Field).Should().Equal(
            "/cronograma/fases/0/tipoFaseCodigo", "/cronograma/fases/0/unidadeResponsavelCodigo",
            "/cronograma/fases/0/publicacoesPrevistas/3/atoCodigo");
        lidos.Origem.PartesRecusadas.Should().OnlyContain(parte => parte.Caminho == "fases" && parte.Chave == "fase" && parte.PosicaoDeOrigem == 0);
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITAD", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "A fase recusada no meio deixa lacuna na ordem e não renumera a que vem depois")]
    public void FaseRecusadaNoMeio_DeixaLacunaNaOrdem()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao"), Fase(1, "digitada", "FASE-DIGITADA"), Fase(2, "resultado", "RESULTADO")));

        lidos.Cronograma.CronogramaFases.Select(static fase => fase.Ordem).Should().Equal(1, 3);
        Cronograma(lidos).ConferirEstrutura().Select(static causa => causa.Error.Code).Should().NotContain("ProcessoSeletivo.OrdemFaseDuplicada");
    }

    [Fact(DisplayName = "A parte recusada de uma fase aponta a posição dela no documento, e não a posição canônica")]
    public void ParteRecusada_ApontaAPosicaoDeOrigem()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "digitada", "FASE-DIGITADA"), Fase(1, "resultado", "RESULTADO", "UNIDADE-DIGITADA")));

        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/tipoFaseCodigo", FaseCronogramaErrorCodes.FaseCanonicaNaoEncontrada),
            ("/cronograma/fases/1/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada));
        ParteRecusada daUnidade = lidos.Origem.PartesRecusadas[1];
        daUnidade.Caminho.Should().Be("fases[0].unidadeResponsavel");
        daUnidade.PosicaoDeOrigem.Should().Be(1);
    }

    [Fact(DisplayName = "Duas causas de configuração em fases diferentes acumulam, cada uma no ponteiro da fase que a tem")]
    public void CausasEmFasesDiferentes_Acumulam()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(
            Fase(0, "inscricao", unidade: "UNIDADE-DIGITADA"),
            Fase(1, "recurso", "RECURSO"),
            Fase(2, "resultado", "RESULTADO", unidade: "OUTRA-DIGITADA")));

        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada),
            ("/cronograma/fases/2/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada));
    }

    [Fact(DisplayName = "A fase sem unidade que a leitura recusou não é recusada de novo como sem unidade pela completude")]
    public void UnidadeInexistente_NaoSaiEmDobroNaCompletude()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao", unidade: "UNIDADE-DIGITADA"), Fase(1, "resultado", "RESULTADO", unidade: null)));

        CronogramaDoProcesso cronograma = Cronograma(lidos);

        cronograma.ConferirCompletude(Utc).Where(static causa => causa.Error.Code == "ProcessoSeletivo.FaseSemUnidadeResponsavel")
            .Select(static causa => causa.Field).Should().Equal("fases[1].unidadeResponsavel");
    }
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodecTipos;

public sealed class EnvelopeCodecTiposRegrasEColunasTests
{
    /// <summary>
    /// O rol que o decodificador aceita para <c>regraDistribuicao.codigo</c>
    /// deriva de <see cref="RegraDistribuicaoVagasCodigo.Todos"/> — todo código do catálogo, não
    /// só os dois que <c>ProcessoRico</c> exercita. Um rol fechado em literais deixaria
    /// irreidratável o envelope publicado sob PSIQ ou sob COM-AC-PCD.
    /// </summary>
    /// <remarks>
    /// O resto do payload continua descrevendo a Lei 12.711 (9 modalidades federais) — incompatível
    /// com o rol restrito de PSIQ e de COM-AC-PCD — então a reidratação ainda falha adiante, no
    /// domínio. O que a asserção prova é que ela não falha MAIS CEDO, no gate do decoder, com o
    /// código recusado por não pertencer ao rol conhecido.
    /// </remarks>
    [Theory(DisplayName = "PSIQ e COM-AC-PCD no bloco distribuicao não são recusados como RegraDesconhecida")]
    [InlineData(RegraDistribuicaoVagasCodigo.Psiq)]
    [InlineData(RegraDistribuicaoVagasCodigo.ComAcPcd)]
    public void CodigoDeQuadroFixoDoSeed_NaoFicaMaisForaDoRolAceitoPeloDecoder(string codigo)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["regraDistribuicao"]!["codigo"] = codigo);

        resultado.IsFailure.Should().BeTrue(
            "o payload continua descrevendo a Lei 12.711 — incompatível com o rol restrito de PSIQ/COM-AC-PCD");
        resultado.Error!.Code.Should().NotBe(
            ErrosCodecEnvelope.RegraDesconhecida,
            $"'{codigo}' pertence a RegraDistribuicaoVagasCodigo.Todos — o decoder não pode mais recusá-lo como fora do rol conhecido");
    }

    /// <summary>
    /// O rol de LEI-12711-COM-AC-PCD é a Lei 12.711 pura + AC_PCD — um SUPERSET, não um
    /// subconjunto. O payload de <c>ProcessoRico</c> (9 modalidades federais, sem AC_PCD)
    /// atravessa o gate do decoder (não é mais <c>RegraDesconhecida</c>), mas falha adiante no
    /// domínio por faltar justamente a décima modalidade que só esta variação exige — o mesmo
    /// desfecho de PSIQ/COM-AC-PCD, ainda que pelo lado oposto do rol (falta, não excedente).
    /// </summary>
    [Fact(DisplayName = "LEI-12711-COM-AC-PCD no bloco distribuicao atravessa o gate do decoder — não é mais recusada como RegraDesconhecida")]
    public void CodigoLei12711ComAcPcd_NaoFicaMaisForaDoRolAceitoPeloDecoder()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["regraDistribuicao"]!["codigo"] = RegraDistribuicaoVagasCodigo.Lei12711ComAcPcd);

        resultado.IsFailure.Should().BeTrue(
            "o payload de ProcessoRico não declara AC_PCD — a variação -COM-AC-PCD exige as dez modalidades do seu próprio rol");
        resultado.Error!.Code.Should().NotBe(
            ErrosCodecEnvelope.RegraDesconhecida,
            "LEI-12711-COM-AC-PCD pertence a RegraDistribuicaoVagasCodigo.Todos — o decoder não pode mais recusá-la como fora do rol conhecido");
    }

    /// <summary>
    /// <c>criteriosDesempate[].parametros.valor</c> (ADR-0111, Story #847) não passa por
    /// <c>leitor.TextoNaoVazio</c> — <c>Valor</c> é polimórfico (escalar ou array, via
    /// <c>leitor.Valor</c>) — mas <c>CondicaoDnf.Criar</c> recusa uma string em branco como
    /// forma incoerente com o operador, com o código de domínio (não o genérico
    /// <c>EnvelopeMalformado</c>): é a mesma família de recusa nomeada (nunca 500), só que
    /// mais específica, propagada pelo mesmo caminho (<see cref="LeitorEnvelope.Propagar{T}"/>).
    /// </summary>
    [Fact(DisplayName = "Texto vazio em criteriosDesempate.parametros.valor é recusado pelo código de domínio de CondicaoDnf")]
    public void TextoVazio_NoValorDoPredicado_RecusaComCodigoDeDominio()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, "criteriosDesempate.3.parametros.valor", "   "));

        resultado.IsFailure.Should().BeTrue(
            "um valor em branco no predicado de fato faria round-trip PERFEITO e restauraria um desempate que não decide nada");
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    /// <summary>
    /// O valor cabe na leitura, satisfaz o domínio e <b>recanonicaliza nos mesmos bytes</b> —
    /// a prova de round-trip <b>aprova</b>. A recusa só chegaria no <c>SaveChanges</c>, como
    /// <c>DbUpdateException</c> (22001 / 22003) no meio do descarte: <b>500 não tratado</b>.
    /// </summary>
    [Theory(DisplayName = "Texto que NÃO CABE NA COLUNA é recusado pelo decoder, não pelo Postgres")]
    [InlineData("etapas.0.nome", 301)]
    [InlineData("modalidades.0.codigo", 61)]
    [InlineData("modalidades.0.descricao", 301)]
    [InlineData("modalidades.0.baseLegal", 501)]
    [InlineData("modalidades.1.acaoQuandoIndeferido", 31)]
    [InlineData("atendimento.condicoes.0.condicaoCodigo", 51)]
    [InlineData("atendimento.condicoes.0.condicaoNome", 301)]
    [InlineData("atendimento.recursos.0.recursoCodigo", 51)]
    [InlineData("bonusRegional.municipioConvenio", 201)]
    [InlineData("bonusRegional.baseLegal", 501)]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia", 21)]
    public void TextoAlemDoLimiteDaColuna_Recusa(string caminho, int comprimento)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, caminho, new string('X', comprimento)));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' com {comprimento} caracteres não cabe na coluna. Sem a guarda, ele atravessaria a leitura, " +
            "satisfaria o domínio, recanonicalizaria nos MESMOS bytes (a prova de round-trip aprovaria) e só " +
            "estouraria no SaveChanges — 500 no meio do descarte, em vez de recusa nomeada.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Decimal com escala impecável mas DÍGITOS DEMAIS para a coluna é recusado")]
    // numeric(18,4) — 19 dígitos não cabem.
    [InlineData("etapas.0.peso", "123456789012345.6789")]
    [InlineData("etapas.0.notaMinima", "123456789012345.6789")]
    // numeric(6,4) — o bônus comporta no máximo 99.9999.
    [InlineData("bonusRegional.fator", "100.0000")]
    [InlineData("bonusRegional.teto", "12345.6789")]
    public void DecimalAlemDaPrecisaoDaColuna_Recusa(string caminho, string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, valor));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' = {valor} tem a escala canônica correta — e é justamente por isso que ele passaria: o " +
            "encoder o reemite idêntico, e a prova de round-trip aprova. O numeric(p,s) só estouraria no " +
            "SaveChanges (22003), no meio do descarte.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}

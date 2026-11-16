namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.CenarioDeConformidadeLegal;

/// <summary>
/// Avaliação de <see cref="DocumentoObrigatorioParaModalidade"/> (CA-09): a modalidade exige o documento quando
/// alguma exigência do tipo pedido cobre, de forma incondicional e determinante, toda a modalidade.
/// </summary>
public sealed class AvaliadorDocumentoObrigatorioCoberturaTests
{
    private const string Tipo = "COMPROVANTE_RESIDENCIA";

    private static ObrigatoriedadeLegal Regra() => NovaRegra("DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", Tipo));

    private static ProcessoSeletivo ProcessoCom(Func<Guid, DocumentoExigido> exigencia)
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirDocumentos(processo, exigencia(PrepararProcessoComModalidade(processo, "LB_PPI")));
        return processo;
    }

    [Fact(DisplayName = "CA-09: modalidade não ofertada por nenhuma oferta do processo aprova vazio — nada a exigir")]
    public void DocumentoObrigatorioParaModalidade_ModalidadeNaoOfertada_Aprova()
    {
        RegraAvaliada avaliada = Avaliar(NovoProcesso(), Regra());

        avaliada.Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: modalidade ofertada sem NENHUMA exigência do tipo pedido reprova, nomeando modalidade e tipo")]
    public void DocumentoObrigatorioParaModalidade_SemExigenciaDoTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        PrepararProcessoComModalidade(processo, "LB_PPI");

        RegraAvaliada avaliada = Avaliar(processo, Regra());

        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain(Tipo, "a reprovação nomeia a modalidade e o tipo de documento, não só um booleano");
    }

    [Fact(DisplayName = "CA-09: exigência GERAL do tipo pedido aprova — cobre qualquer modalidade, por definição")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaGeralDoTipo_Aprova()
    {
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaGeral(fase, Tipo));

        Avaliar(processo, Regra()).Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: exigência do tipo pedido que NÃO determina resultado (opcional, sem consequência) reprova")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaOpcionalDoTipo_Reprova()
    {
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaGeral(fase, Tipo, obrigatorio: false));

        Avaliar(processo, Regra()).Aprovada.Should().BeFalse(
            "uma exigência que não determina resultado (Obrigatorio=false, sem ConsequenciaIndeferimento) é meramente opcional " +
            "— não satisfaz a obrigação legal \"a modalidade X DEVE exigir o documento Y\"");
    }

    [Fact(DisplayName = "CA-09: exigência CONDICIONAL com gatilho MODALIDADE = X (só esse fato) aprova — cobre a modalidade incondicionalmente")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaCondicionalSoPelaModalidade_Aprova()
    {
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaCondicionalPorModalidade(fase, Tipo, "LB_PPI"));

        Avaliar(processo, Regra()).Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: exigência pelo grupo da convocação cobre a modalidade — quem foi convocado nela também concorre nela")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaPeloGrupoDaConvocacao_Aprova()
    {
        ProcessoSeletivo processo = ProcessoCom(
            fase => ExigenciaCondicionalPorModalidade(fase, Tipo, "LB_PPI", fatoDeModalidade: "MODALIDADE_CONVOCACAO"));

        Avaliar(processo, Regra()).Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: exigência CONDICIONAL cujo gatilho também depende de outro fato reprova — a cobertura da modalidade não é incondicional")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaCondicionalComFatoExtra_Reprova()
    {
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaCondicionalPorModalidade(fase, Tipo, "LB_PPI", fatoExtra: "FAIXA_ETARIA"));

        Avaliar(processo, Regra()).Aprovada.Should().BeFalse(
            "a exigência só cobre quem também satisfaz FAIXA_ETARIA — nem todo candidato de LB_PPI seria coberto, " +
            "e a obrigação legal não admite exceção");
    }

    [Fact(DisplayName = "Story #916: gatilho Indeterminado (fato extra não resolvido pelo fato sintético) não conta como cobertura incondicional")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaComGatilhoIndeterminado_Reprova()
    {
        // A avaliação usa um fato sintético só com MODALIDADE: FAIXA_ETARIA nunca está presente, então o gatilho
        // avalia Indeterminado (fail-closed). A conclusão é a de Falso: não prova incondicionalidade.
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaCondicionalPorModalidade(fase, Tipo, "LB_PPI", fatoExtra: "FAIXA_ETARIA"));

        Avaliar(processo, Regra()).Aprovada.Should().BeFalse("Indeterminado conta como \"não provado\", a mesma conclusão que Falso");
    }

    [Fact(DisplayName = "CA-09: exigência do TIPO ERRADO não conta, mesmo cobrindo a modalidade corretamente")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaDeOutroTipo_Reprova()
    {
        ProcessoSeletivo processo = ProcessoCom(fase => ExigenciaCondicionalPorModalidade(fase, "LAUDO_MEDICO", "LB_PPI"));

        Avaliar(processo, Regra()).Aprovada.Should().BeFalse();
    }
}

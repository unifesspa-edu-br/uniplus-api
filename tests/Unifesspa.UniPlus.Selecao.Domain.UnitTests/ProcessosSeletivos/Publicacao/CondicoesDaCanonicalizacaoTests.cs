namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CondicoesDaCanonicalizacaoTests
{
    private const string TermoSemFormaDeAceite = "ProcessoSeletivo.TermoExigidoSemFormaDeAceite";
    private const string FatoSemValoresOfertados = "ProcessoSeletivo.FatoColetadoSemValoresOfertados";

    [Fact(DisplayName = "O processo conforme não tem causa a corrigir antes de canonicalizar")]
    public void ProcessoConforme_NaoTemCausa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        CondicoesDaCanonicalizacao.Causas(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As conferências independentes acumulam as causas, na ordem em que são aplicadas, cada uma no bloco do documento")]
    public void ConferenciasIndependentes_AcumulamAsCausasNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirItensComFaseDeInscricao([FatoDosMunicipiosDoFada()]).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([TermoSemForma()]).IsSuccess.Should().BeTrue();

        IReadOnlyList<FieldError> causas = CondicoesDaCanonicalizacao.Causas(processo.Declaracao(), CadastrosDeTeste.Para(processo));

        causas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("formularios", FatoSemValoresOfertados),
            ("formularios", TermoSemFormaDeAceite));
    }

    [Fact(DisplayName = "A causa de uma regra não esconde a das outras: a regra que lê o dado recusado é que se guarda")]
    public void CausaDeUmaRegra_NaoEscondeAsOutras()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirItensComFaseDeInscricao([FatoDosMunicipiosDoFada()]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacao.Causas(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Select(static causa => causa.Error.Code).Should().Equal(FatoSemValoresOfertados);
    }

    private static TermoExigidoFormulario TermoSemForma() => TermoExigidoFormulario.Criar(
        "DECLARACAO", 0,
        new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", FormaAceite.ADefinir, new string('a', 64)),
        exibicao: null, Obrigatoriedade.Sempre).Value!;

    private static FatoColetado FatoDosMunicipiosDoFada() => FatoColetadoDeTeste.Coletando("MUNICIPIO_EM_AREA_FADA", 0)
        .ComRotulo("Município")
        .DoTipo(TipoDeCampo.EscolhaUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComOrigem(OrigemValoresColeta.MunicipiosDoFada)
        .Construir();
}

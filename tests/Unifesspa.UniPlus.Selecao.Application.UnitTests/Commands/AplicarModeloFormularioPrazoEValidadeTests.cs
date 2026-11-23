namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// O prazo de emissão e a validade que o modelo escolhe para um documento: a cópia para o processo troca o
/// código da fase de cada referência pela fase do cronograma, deixa sem escolha o que o modelo não escolheu e
/// recusa a fase que o cronograma não tem.
/// </summary>
public sealed class AplicarModeloFormularioPrazoEValidadeTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    private static DocumentoExigidoDoModeloInput LaudoComRequisitos(RequisitosDoDocumentoDoModeloInput? requisitos) =>
        DocumentoDoModelo("LAUDO_MEDICO", "PCD") with { Requisitos = requisitos };

    private static string CamposRecusados(Result<AplicacaoDeModeloDto> resultado) =>
        string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}"));

    private Guid IdDaFase(string codigo) => _cenario.Processo.CronogramaFases.Single(f => f.Codigo == codigo).Id;

    private void DevePedirPrazoDe90DiasAPartirDoFimDaInscricao(PrazoDeEmissao? prazo)
    {
        prazo!.Escolha.Should().Be(EscolhaDoPrazoDeEmissao.ComPrazo);
        prazo.Valor.Should().Be(90);
        prazo.Unidade.Should().Be(UnidadeDoPrazoDeEmissao.Dias);
        prazo.EfeitoForaDoPrazo.Should().Be(EfeitoNaAnaliseDoDocumento.RecusarDocumento);
        prazo.Referencia!.Tipo.Should().Be(TipoDaDataDeReferencia.FimDaFase);
        prazo.Referencia.FaseId.Should().Be(IdDaFase("INSCRICAO"));
    }

    private void DeveExigirValidadeNoInicioDoResultadoFinal(ValidadeDoDocumento? validade)
    {
        validade!.Escolha.Should().Be(EscolhaDaValidade.ExigeDocumentoValido);
        validade.EfeitoDoVencimento.Should().Be(EfeitoNaAnaliseDoDocumento.RecusarDocumento);
        validade.Referencia!.Tipo.Should().Be(TipoDaDataDeReferencia.InicioDaFase);
        validade.Referencia.FaseId.Should().Be(IdDaFase("RESULTADO_FINAL"));
    }

    private void DeclararCronograma()
    {
        _cenario.FaseDoCronograma("INSCRICAO", "RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
    }

    [Fact(DisplayName = "O prazo de emissão e a validade escolhidos pelo modelo são copiados com a fase de cada referência trocada pela do cronograma")]
    public async Task Handle_ModeloComPrazoEValidade_CopiaComAFaseDoCronograma()
    {
        DeclararCronograma();
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoFimDaFase("INSCRICAO"))
            .ExigindoValidadeNa(RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("RESULTADO_FINAL"), "RECUSAR_DOCUMENTO").Construir();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(LaudoComRequisitos(requisitos)));

        resultado.IsSuccess.Should().BeTrue(CamposRecusados(resultado));
        DocumentoExigido laudo = _cenario.Processo.DocumentosExigidos.Single();
        DevePedirPrazoDe90DiasAPartirDoFimDaInscricao(laudo.PrazoDeEmissao);
        DeveExigirValidadeNoInicioDoResultadoFinal(laudo.Validade);
    }

    [Fact(DisplayName = "A referência que não usa fase é copiada sem fase, e o que o modelo não escolheu fica sem escolha no processo")]
    public async Task Handle_ReferenciaSemFaseEEscolhaAusente_CopiaSemFaseESemEscolha()
    {
        DeclararCronograma();
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(2, "ANOS", RequisitosDoDocumentoDoModeloBuilder.NoFimDasInscricoes()).Construir();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(LaudoComRequisitos(requisitos)));

        resultado.IsSuccess.Should().BeTrue(CamposRecusados(resultado));
        DocumentoExigido laudo = _cenario.Processo.DocumentosExigidos.Single();
        laudo.PrazoDeEmissao!.Referencia!.Tipo.Should().Be(TipoDaDataDeReferencia.FimDasInscricoes);
        laudo.PrazoDeEmissao.Referencia.FaseId.Should().BeNull();
        laudo.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "O modelo sem requisitos copia o documento sem prazo de emissão nem validade")]
    public async Task Handle_ModeloSemRequisitos_CopiaSemEscolha()
    {
        DeclararCronograma();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(LaudoComRequisitos(null)));

        resultado.IsSuccess.Should().BeTrue(CamposRecusados(resultado));
        DocumentoExigido laudo = _cenario.Processo.DocumentosExigidos.Single();
        laudo.PrazoDeEmissao.Should().BeNull();
        laudo.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "A fase de uma referência que o cronograma não tem é recusada no campo dela, e nada é gravado")]
    public async Task Handle_ReferenciaEmFaseInexistente_RecusaSemGravar()
    {
        DeclararCronograma();
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoFimDaFase("HABILITACAO"))
            .ExigindoValidadeNa(RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("MATRICULA")).Construir();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(LaudoComRequisitos(requisitos)));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("modelo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.referencia.faseCodigo", AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente),
            ("modelo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.validade.referencia.faseCodigo", AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente));
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A fase da referência e a do próprio documento ausentes do cronograma são recusadas juntas")]
    public async Task Handle_FaseDoDocumentoEDaReferenciaInexistentes_RecusaAsDuas()
    {
        DeclararCronograma();
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoFimDaFase("HABILITACAO")).Construir();
        DocumentoExigidoDoModeloInput documento = LaudoComRequisitos(requisitos) with { FaseCodigo = "MATRICULA" };

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(documento));

        resultado.Errors.Select(static e => e.Field).Should().Equal(
            "modelo.conjuntoDeDocumentos.documentosExigidos[0].faseCodigo", "modelo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.referencia.faseCodigo");
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegraDeComprovacaoTests
{
    private static DocumentoExigido Documento(
        Guid? faseId = null, string? consequencia = null, bool obrigatorio = true, Aplicabilidade aplicabilidade = Aplicabilidade.Geral,
        FinalidadeFormulario? finalidade = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId ?? Guid.CreateVersion7(), null, FormularioDeTeste.CodigoDoPapel(finalidade)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(aplicabilidade, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, consequencia, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static BaseLegalDaComprovacao BaseLegalResolvida() =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;

    [Theory(DisplayName = "CriarGrupo recusa grupo sem itens em qualquer forma")]
    [InlineData(FormaDeComprovacao.TodosOsItens)]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens)]
    public void CriarGrupo_SemItens_Recusa(FormaDeComprovacao forma)
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(forma, null, null), 0, [], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.GrupoSemItens");
    }

    [Fact(DisplayName = "CriarGrupo recusa itens de fases diferentes")]
    public void CriarGrupo_FasesDiferentes_Recusa()
    {
        RegraDeComprovacao primeiraRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(Guid.CreateVersion7()), 0).Value!;
        RegraDeComprovacao segundaRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(Guid.CreateVersion7()), 1).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [primeiraRegraDeDocumento, segundaRegraDeDocumento]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.GrupoComFasesDiferentes");
    }

    [Fact(DisplayName = "CriarGrupo aceita itens da mesma fase")]
    public void CriarGrupo_MesmaFase_Aceita()
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao primeiraRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 0).Value!;
        RegraDeComprovacao segundaRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 1).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [primeiraRegraDeDocumento, segundaRegraDeDocumento]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.FaseComum().Should().Be(faseId);
    }

    [Theory(DisplayName = "CriarGrupo aceita só documentos do mesmo formulário, inclusive todos fora de formulário")]
    [InlineData(FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, false)]
    [InlineData(FinalidadeFormulario.Inscricao, null, false)]
    [InlineData(null, null, true)]
    public void CriarGrupo_MesmaFinalidade(FinalidadeFormulario? primeira, FinalidadeFormulario? segunda, bool aceita)
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao primeiraRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, finalidade: primeira), 0).Value!;
        RegraDeComprovacao segundaRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId, finalidade: segunda), 1).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [primeiraRegraDeDocumento, segundaRegraDeDocumento]);

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Error!.Code.Should().Be("RegraDeComprovacao.GrupoComFormulariosDiferentes");
        }
    }

    [Fact(DisplayName = "CriarGrupo recusa ciclo (identidade de referência) — defesa em profundidade de chamador interno")]
    public void CriarGrupo_ComCiclo_Recusa()
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 0).Value!;

        // A mesma regra não pode aparecer mais de uma vez na composição.
        // é de domínio, não só de contrato (ver remarks de RegraDeComprovacao.CriarGrupo).
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [item, item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.RegraContemASiMesma");
    }

    [Fact(DisplayName = "CriarGrupo TodosOsItens recusa quantidadeMinima")]
    public void CriarGrupo_TodosOsItens_ComQuantidadeMinima_Recusa()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, 1, null), 0, [], [item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos");
    }

    [Fact(DisplayName = "CriarGrupo TodosOsItens recusa consequência")]
    public void CriarGrupo_TodosOsItens_ComConsequencia_Recusa()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, "ELIMINA_DO_PROCESSO"), 0, [], [item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos");
    }

    [Fact(DisplayName = "CriarGrupo TodosOsItens recusa base legal")]
    public void CriarGrupo_TodosOsItens_ComBaseLegal_Recusa()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), 0, [BaseLegalResolvida()], [item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos");
    }

    [Theory(DisplayName = "CriarGrupo QuantidadeMinimaDeItens recusa quantidadeMinima fora dos limites")]
    [InlineData(0)]
    [InlineData(3)]
    public void CriarGrupo_QuantidadeMinimaDeItens_QuantidadeMinimaForaDosLimites_Recusa(int quantidadeMinima)
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao primeiraRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 0).Value!;
        RegraDeComprovacao segundaRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 1).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, quantidadeMinima, null), 0, [], [primeiraRegraDeDocumento, segundaRegraDeDocumento]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites");
    }

    [Fact(DisplayName = "CriarGrupo QuantidadeMinimaDeItens sem quantidadeMinima aplica o mínimo padrão de 1")]
    public void CriarGrupo_QuantidadeMinimaDeItens_SemQuantidadeMinima_AplicaMinimoPadrao()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [item]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.QuantidadeMinima.Should().Be(RegraDeComprovacao.QuantidadeMinimaPadrao);
    }

    [Fact(DisplayName = "CriarGrupo QuantidadeMinimaDeItens recusa consequência fora do vocabulário fechado")]
    public void CriarGrupo_QuantidadeMinimaDeItens_ConsequenciaInvalida_Recusa()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, "QUALQUER_COISA"), 0, [], [item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.ConsequenciaInvalida");
    }

    [Fact(DisplayName = "CriarGrupo QuantidadeMinimaDeItens recusa base legal sem consequência")]
    public void CriarGrupo_QuantidadeMinimaDeItens_BaseLegalSemConsequencia_Recusa()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [BaseLegalResolvida()], [item]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.BaseLegalSemConsequencia");
    }

    [Fact(DisplayName = "CriarGrupo QuantidadeMinimaDeItens com consequência e base legal")]
    public void CriarGrupo_QuantidadeMinimaDeItens_ComConsequenciaEBaseLegal_Aceita()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(

            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), 0, [BaseLegalResolvida()], [item]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConsequenciaDoDescumprimento.Should().Be(Unifesspa.UniPlus.Regras.Enums.ConsequenciaDoDescumprimento.EliminaDoProcesso);
        resultado.Value!.BasesLegais.Should().ContainSingle();
        resultado.Value!.DeterminaResultado().Should().BeTrue();
    }

    [Fact(DisplayName = "CriarGrupo recusa repetição dentro de repetição — grupo repetido com regra de documento também repetida")]
    public void CriarGrupo_RepeticaoDentroDeRepeticaoDireta_Recusa()
    {
        RegraDeComprovacao regraDeDocumentoRepetida = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(

            new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [regraDeDocumentoRepetida], grupoDoFormularioParaRepeticao: "PESSOAS_JURIDICAS");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.RepeticaoDentroDeRepeticao");
    }

    [Fact(DisplayName = "CriarGrupo recusa repetição dentro de repetição mesmo profunda (grupo repetido → grupo de todos os itens → regra de documento repetida)")]
    public void CriarGrupo_RepeticaoDentroDeRepeticaoProfunda_Recusa()
    {
        RegraDeComprovacao regraDeDocumentoRepetida = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;
        RegraDeComprovacao grupoIntermediario = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [regraDeDocumentoRepetida]).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(

            new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [grupoIntermediario], grupoDoFormularioParaRepeticao: "PESSOAS_JURIDICAS");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.RepeticaoDentroDeRepeticao");
    }

    [Fact(DisplayName = "CriarGrupo aceita grupo repetido cujos itens NÃO repetem")]
    public void CriarGrupo_GrupoDoFormularioParaRepeticao_ComItensNaoRepetidos_Aceita()
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao primeiraRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 0).Value!;
        RegraDeComprovacao segundaRegraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 1).Value!;

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarGrupo(

            new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [primeiraRegraDeDocumento, segundaRegraDeDocumento], grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Grupo que exige todos os itens não determina resultado próprio")]
    public void DeterminaResultado_TodosOsItens_SempreFalso()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [item]).Value!;

        grupoDeTodos.DeterminaResultado().Should().BeFalse();
    }

    [Fact(DisplayName = "Grupo sem consequência própria não determina resultado")]
    public void DeterminaResultado_QuantidadeMinimaDeItensSemConsequencia_Falso()
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), 0).Value!;
        RegraDeComprovacao grupoDeQuantidadeMinima = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [item]).Value!;

        grupoDeQuantidadeMinima.DeterminaResultado().Should().BeFalse();
    }

    [Fact(DisplayName = "EnumerarRegraEItens devolve a regra e todos os itens contidos")]
    public void EnumerarRegraEItens_RegrasDentroDeGrupo_DevolveTodasAsRegras()
    {
        Guid faseId = Guid.CreateVersion7();
        RegraDeComprovacao rg = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 0).Value!;
        RegraDeComprovacao cpf = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 1).Value!;
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [rg, cpf]).Value!;
        RegraDeComprovacao cin = RegraDeComprovacao.CriarRegraDeDocumento(Documento(faseId), 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, null, null), 0, [], [grupoDeTodos, cin]).Value!;

        List<RegraDeComprovacao> regras = [.. regraSemGrupo.EnumerarRegraEItens()];

        regras.Should().HaveCount(5);
        regras.Should().Contain(new[] { regraSemGrupo, grupoDeTodos, rg, cpf, cin });
    }

    [Fact(DisplayName = "PodeAlcancarModalidade em grupo é a união das modalidades alcançadas pelas regras de documento")]
    public void PodeAlcancarModalidade_Grupo_UniaoDasRegrasDeDocumento()
    {
        Guid faseId = Guid.CreateVersion7();
        DocumentoExigido documentoGeral = Documento(faseId, aplicabilidade: Aplicabilidade.Geral);
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(documentoGeral, 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [regraDeDocumento]).Value!;

        // Aplicabilidade GERAL alcança qualquer modalidade (DocumentoExigido.PodeAlcancarModalidade).
        grupo.PodeAlcancarModalidade("AC", FatosDeModalidadeDeTeste.DoCatalogo).Should().BeTrue();
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura da etapa em que o documento é coletado: ela precisa existir no processo, acontecer
/// na fase declarada pela exigência e não pode ser removida nem trocar de fase enquanto coleta.
/// </summary>
public sealed class ProcessoSeletivoDocumentosExigidosNaEtapaTests
{
    private static DocumentoExigido ExigenciaNaEtapa(Guid exigidoNaFaseId, Guid exigidoNaEtapaId, FinalidadeFormulario? finalidade = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, exigidoNaEtapaId, FormularioDeTeste.CodigoDoPapel(finalidade)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static EtapaProcesso EtapaMovidaParaAFase(EtapaProcesso etapa, string faseCodigo) =>
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            etapa.Id,
            new IdentificacaoDaEtapa(Nome: etapa.Nome, Carater: etapa.Carater, TipoEtapa: etapa.TipoEtapa, Ordem: etapa.Ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: etapa.Peso, NotaMinima: etapa.NotaMinima, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    [Fact(DisplayName = "Etapa da própria fase coleta o documento")]
    public void DefinirDocumentosExigidos_EtapaDaPropriaFase_Aceita()
    {
        FaseCronograma fase = Fase(1, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        EtapaProcesso etapa = Etapa("Comprovação de renda", "HABILITACAO", 1);
        AdicionarEtapas(processo, etapa);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Habilitacao);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaNaEtapa(fase.Id, etapa.Id, FinalidadeFormulario.Habilitacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.ExigidoNaEtapaId == etapa.Id);
    }

    [Fact(DisplayName = "Etapa que não pertence ao processo é recusada")]
    public void DefinirDocumentosExigidos_EtapaDeOutroProcesso_Recusa()
    {
        FaseCronograma fase = Fase(1, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaNaEtapa(fase.Id, Guid.CreateVersion7(), FinalidadeFormulario.Habilitacao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAoProcesso");
    }

    /// <summary>
    /// A etapa existe, mas acontece em outra fase — apontá-la diria que a habilitação
    /// coleta o documento no dia da prova.
    /// </summary>
    [Fact(DisplayName = "Etapa de outra fase do mesmo processo é recusada")]
    public void DefinirDocumentosExigidos_EtapaDeOutraFase_Recusa()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(habilitacao, Fase(2, "AVALIACAO"));
        EtapaProcesso prova = Etapa("Prova objetiva", "AVALIACAO", 1);
        AdicionarEtapas(processo, prova);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaNaEtapa(habilitacao.Id, prova.Id, FinalidadeFormulario.Habilitacao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAFase");
    }

    /// <summary>
    /// Remover a etapa deixaria a exigência apontando para uma etapa inexistente, e a
    /// coleta sem onde acontecer — mesma proteção que desempate e eliminação já têm.
    /// </summary>
    [Fact(DisplayName = "Etapa que coleta documento não pode ser removida")]
    public void DefinirEtapas_RemoveEtapaQueColetaDocumento_Recusa()
    {
        FaseCronograma fase = Fase(1, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        EtapaProcesso etapa = Etapa("Comprovação de renda", "HABILITACAO", 1);
        AdicionarEtapas(processo, etapa);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Habilitacao);
        ExigirDocumentos(processo, ExigenciaNaEtapa(fase.Id, etapa.Id, FinalidadeFormulario.Habilitacao));

        Result resultado = processo.DefinirEtapas([Etapa("Entrevista", "HABILITACAO", 1)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental");
    }

    /// <summary>
    /// Manter o id não basta: a exigência declara a fase E a etapa em que o documento é
    /// coletado, e mover a etapa para outra fase a deixaria dizendo que a habilitação é
    /// coletada no dia da prova. É a mesma invariante que a definição da exigência impõe.
    /// </summary>
    [Fact(DisplayName = "Etapa que coleta documento não pode mudar de fase")]
    public void DefinirEtapas_MoveEtapaQueColetaDocumentoParaOutraFase_Recusa()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(habilitacao, Fase(2, "AVALIACAO"));
        EtapaProcesso etapa = Etapa("Comprovação de renda", "HABILITACAO", 1);
        AdicionarEtapas(processo, etapa);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Habilitacao);
        ExigirDocumentos(processo, ExigenciaNaEtapa(habilitacao.Id, etapa.Id, FinalidadeFormulario.Habilitacao));

        // A MESMA etapa, com o MESMO id — só a fase mudou. A guarda de remoção não vê nada,
        // porque o id continua na lista.
        Result resultado = processo.DefinirEtapas([EtapaMovidaParaAFase(etapa, "AVALIACAO")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAFase");
    }
}

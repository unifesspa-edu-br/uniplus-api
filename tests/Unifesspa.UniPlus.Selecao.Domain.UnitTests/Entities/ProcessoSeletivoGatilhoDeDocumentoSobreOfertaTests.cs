namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura da guarda de retrocesso: o documento cuja condição de gatilho cita um código de
/// modalidade, de condição de atendimento ou de recurso de acessibilidade impede que a oferta
/// redefinida deixe de incluir esse código.
/// </summary>
public sealed class ProcessoSeletivoGatilhoDeDocumentoSobreOfertaTests
{
    private sealed record TipoDeDocumento(string Codigo, string Nome, string Categoria);

    private static readonly TipoDeDocumento Identidade = new("IDENTIDADE", "Documento de identidade", "PESSOAL");

    private static CondicaoGatilho CondicaoDe(string fato, string valor) => CondicaoGatilho.Criar(
        0, fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    private static ModalidadeSelecionada Modalidade(string codigo) => ModalidadeSelecionada.Criar(
        modalidadeOrigemId: Guid.CreateVersion7(),
        codigo: codigo,
        descricao: null,
        naturezaLegal: NaturezaLegalModalidade.Ampla,
        composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
        composicaoOrigemCodigo: null,
        regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
        remanejamentoDestino: null,
        remanejamentoPar: null,
        remanejamentoFallback: null,
        regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
        acaoQuandoIndeferido: null,
        baseLegal: "Res. Unifesspa 532/2021",
        quantidadeDeclarada: 10).Value!;

    private static ConfiguracaoDistribuicaoVagas DistribuicaoCom(params string[] codigosModalidade)
    {
        ReferenciaRegra regra = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", string.Concat(Enumerable.Repeat("ab01234567", 7))[..64]);
        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: codigosModalidade.Length * 10,
            pr: 1m,
            regraDistribuicao: regra,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [.. codigosModalidade.Select(Modalidade)], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
    }

    private static DocumentoExigido ExigenciaCondicionada(
        Guid faseId, TipoDeDocumento tipoDocumento, CondicaoGatilho condicao) =>
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), tipoDocumento.Codigo, tipoDocumento.Nome, tipoDocumento.Categoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [condicao], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    private static ProcessoSeletivo ProcessoComModalidadeCitadaPelaExigencia(TipoDeDocumento tipoDocumento, string fato)
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        processo.DefinirDistribuicaoVagas([DistribuicaoCom("LB_PPI", "AC")], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ExigirFolhas(processo, ExigenciaCondicionada(fase.Id, tipoDocumento, CondicaoDe(fato, "LB_PPI")));
        return processo;
    }

    private static ProcessoSeletivo ProcessoComAtendimentoCitadoPelaExigencia(
        AtendimentoEspecializado atendimento, TipoDeDocumento tipoDocumento, CondicaoGatilho condicao)
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        processo.DefinirAtendimentoEspecializado(atendimento, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ExigirFolhas(processo, ExigenciaCondicionada(fase.Id, tipoDocumento, condicao));
        return processo;
    }

    private static AtendimentoEspecializado AtendimentoComCondicao(string codigo, string nome) =>
        AtendimentoEspecializado.Criar([CondicaoOfertada.Criar(Guid.CreateVersion7(), codigo, nome)], [], []).Value!;

    private static ProcessoSeletivo ProcessoComCondicaoPcdCitadaPelaExigencia() =>
        ProcessoComAtendimentoCitadoPelaExigencia(
            AtendimentoComCondicao("PCD", "Pessoa com deficiência"),
            new TipoDeDocumento("LAUDO_MEDICO", "Laudo médico", "SAUDE"),
            CondicaoDe("CONDICAO_ATENDIMENTO", "PCD"));

    [Fact(DisplayName = "Redefinir distribuição de vagas removendo código de modalidade referenciado por gatilho vivo é recusado")]
    public void DefinirDistribuicaoVagas_RemoveModalidadeReferenciada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeCitadaPelaExigencia(Identidade, "MODALIDADE");

        Result resultado = processo.DefinirDistribuicaoVagas([DistribuicaoCom("AC")], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue("LB_PPI é referenciada por uma condição de gatilho viva e deixaria de ser ofertada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir a distribuição retirando o grupo citado pela exigência da convocação é recusado")]
    public void DefinirDistribuicaoVagas_RemoveGrupoCitadoPelaConvocacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeCitadaPelaExigencia(
            new TipoDeDocumento("AUTODECLARACAO_PPI", "Autodeclaração étnico-racial", "PESSOAL"), "MODALIDADE_CONVOCACAO");

        Result resultado = processo.DefinirDistribuicaoVagas([DistribuicaoCom("AC")], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir distribuição de vagas preservando o código de modalidade referenciado é aceito (contraprova)")]
    public void DefinirDistribuicaoVagas_PreservaModalidadeReferenciada_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeCitadaPelaExigencia(Identidade, "MODALIDADE");

        Result resultado = processo.DefinirDistribuicaoVagas([DistribuicaoCom("LB_PPI", "AC", "LI_PPI")], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Redefinir atendimento especializado removendo código de condição referenciado por gatilho vivo é recusado")]
    public void DefinirAtendimentoEspecializado_RemoveCondicaoReferenciada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComCondicaoPcdCitadaPelaExigencia();

        Result resultado = processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue("PCD é referenciada por uma condição de gatilho viva e deixaria de ser ofertada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir atendimento especializado removendo código de recurso referenciado por condição viva é recusado")]
    public void DefinirAtendimentoEspecializado_RemoveRecursoReferenciado_Recusa()
    {
        RecursoOfertado salaAmamentacao = RecursoOfertado.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação");
        ProcessoSeletivo processo = ProcessoComAtendimentoCitadoPelaExigencia(
            AtendimentoEspecializado.Criar([], [salaAmamentacao], []).Value!,
            new TipoDeDocumento("CERTIDAO_NASCIMENTO", "Certidão de nascimento da criança", "PESSOAL"),
            CondicaoDe("RECURSO_ACESSIBILIDADE", "SALA_AMAMENTACAO"));

        Result resultado = processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue("a sala para amamentação é citada por uma condição viva e deixaria de ser ofertada");
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RecursoAcessibilidadeReferenciadoPorCondicaoViva);
    }

    [Fact(DisplayName = "Redefinir atendimento especializado preservando o código de condição referenciado é aceito (contraprova)")]
    public void DefinirAtendimentoEspecializado_PreservaCondicaoReferenciada_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComCondicaoPcdCitadaPelaExigencia();

        Result resultado = processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência"), CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante")],
                [], []).Value!,
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}

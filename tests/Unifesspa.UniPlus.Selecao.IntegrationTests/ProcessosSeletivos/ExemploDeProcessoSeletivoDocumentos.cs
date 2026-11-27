namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoVagas;

internal static class ExemploDeProcessoSeletivoDocumentos
{
    // Identificadores fixos permitem comparar os bytes de construções com ordens diferentes.
    internal static IReadOnlyList<RegraDeComprovacao> RegrasDeComprovacaoRicas(int variante, bool permutar)
    {
        return Ordem([RegraDeHistorico(variante), GrupoDeRenda(variante, permutar)], permutar);
    }

    private static RegraDeComprovacao RegraDeHistorico(int variante)
    {
        DocumentoExigido historico = DocumentoExigidoRico(DocumentoExigidoIdFixo(1, variante), FaseInscricao(variante).Id,
            TipoDocumentoOrigemIdFixo(1), "HISTORICO_ESCOLAR", "Histórico escolar do ensino médio", "ACADEMICO", obrigatorio: true);
        return RegraDeComprovacao.ReidratarRegraDeDocumento(RegraDeComprovacaoIdFixo(1, variante), 0, historico, 2, DistincaoDasApresentacoes.Reidratar(
            CriterioDeDistincaoDasApresentacoes.Ocorrencia, null, ["Anuário", "Ana\u0301lise"]), null);
    }

    private static RegraDeComprovacao GrupoDeRenda(int variante, bool permutar)
    {
        Guid fase = FaseInscricao(variante).Id;
        DocumentoExigido declaracao = DocumentoExigidoRico(DocumentoExigidoIdFixo(2, variante), fase, TipoDocumentoOrigemIdFixo(2),
            "DECLARACAO_IMPOSTO_RENDA", "Declaração de Imposto de Renda", "RENDA", obrigatorio: false);
        DocumentoExigido extrato = DocumentoExigidoRico(DocumentoExigidoIdFixo(3, variante), fase, TipoDocumentoOrigemIdFixo(3),
            "EXTRATO_BANCARIO", "Extrato bancário dos últimos três meses", "RENDA", obrigatorio: false);
        RegraDeComprovacao regraDeDeclaracao = RegraDeComprovacao.ReidratarRegraDeDocumento(RegraDeComprovacaoIdFixo(2, variante), 0, declaracao, 1, null, null);
        RegraDeComprovacao regraDeExtrato = RegraDeComprovacao.ReidratarRegraDeDocumento(RegraDeComprovacaoIdFixo(3, variante), 1, extrato, 1, null, null);
        return RegraDeComprovacao.ReidratarGrupo(RegraDeComprovacaoIdFixo(4, variante), 1, new CombinacaoDoGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null),
            [], Ordem([regraDeDeclaracao, regraDeExtrato], permutar), null);
    }

    internal static Guid TipoDocumentoOrigemIdFixo(int indice) =>
        new($"77771111-0000-4000-8000-00000000000{indice:x}");

    internal static Guid DocumentoExigidoIdFixo(int ordem, int variante) =>
        new($"7777200{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static Guid RegraDeComprovacaoIdFixo(int ordem, int variante) =>
        new($"7777300{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static DocumentoExigido DocumentoExigidoRico(
        Guid id, Guid exigidoNaFaseId, Guid tipoDocumentoOrigemId, string tipoDocumentoCodigo,
        string tipoDocumentoNome, string tipoDocumentoCategoria, bool obrigatorio) =>
        DocumentoExigido.Reidratar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_" + tipoDocumentoCodigo.ToLowerInvariant()).Value!,
            id,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(tipoDocumentoOrigemId, tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(exigidoNaFaseId, null, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao)),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(obrigatorio, null, []),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!),
                null));

    /// <summary>
    /// Id fixo de produto — como o das etapas e o das fases, ele entra no envelope, e sortear
    /// um Guid novo a cada montagem faria os bytes nunca baterem entre duas construções do
    /// mesmo exemplo.
    /// </summary>
}

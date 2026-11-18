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
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeVagas;

internal static class CorpusEnvelopeDocumentos
{
    /// <summary>
    /// A árvore de satisfação exercitada pela prova de permutação (Story #928, §7.5, issue
    /// #1087): DUAS raízes — uma folha solteira (<c>HISTORICO_ESCOLAR</c>, exigência "solteira",
    /// sem grupo) e um grupo <c>OU</c> com DOIS filhos (comprovação de renda por declaração de
    /// imposto de renda OU extrato bancário). Duas raízes de um filho cada deixariam a coleção
    /// <c>filhos</c> trivial — qualquer ordenação produziria o mesmo resultado, e a permutação
    /// não provaria nada sobre a ordenação de <c>arvoreSatisfacao[].filhos</c>.
    /// </summary>
    /// <remarks>
    /// Ids <b>fixos</b>, via <c>Reidratar</c> — mesma razão de <see cref="EtapaId"/>: tanto
    /// <c>DocumentoExigido.Id</c> quanto <c>NoExigencia.Id</c> entram no envelope
    /// (<c>exigenciaId</c>/<c>id</c>). <c>Criar</c> sorteia um Guid v7 novo a cada chamada — duas
    /// montagens desta árvore (uma para o processo direto, outra para o permutado) produziriam
    /// ids distintos e os bytes nunca bateriam, mesmo sem nenhuma diferença de ORDEM.
    /// </remarks>
    internal static IReadOnlyList<NoExigencia> ArvoreSatisfacaoRica(int variante, bool permutar)
    {
        return Ordem([FolhaDeHistorico(variante), GrupoDeRenda(variante, permutar)], permutar);
    }

    private static NoExigencia FolhaDeHistorico(int variante)
    {
        DocumentoExigido historico = DocumentoExigidoRico(DocumentoExigidoIdFixo(1, variante), FaseInscricao(variante).Id,
            TipoDocumentoOrigemIdFixo(1), "HISTORICO_ESCOLAR", "Histórico escolar do ensino médio", "ACADEMICO", obrigatorio: true);
        return NoExigencia.ReidratarFolha(NoExigenciaIdFixo(1, variante), 0, historico, NoExigencia.QuantidadeMinimaPadrao, null, null);
    }

    private static NoExigencia GrupoDeRenda(int variante, bool permutar)
    {
        Guid fase = FaseInscricao(variante).Id;
        DocumentoExigido declaracao = DocumentoExigidoRico(DocumentoExigidoIdFixo(2, variante), fase, TipoDocumentoOrigemIdFixo(2),
            "DECLARACAO_IMPOSTO_RENDA", "Declaração de Imposto de Renda", "RENDA", obrigatorio: false);
        DocumentoExigido extrato = DocumentoExigidoRico(DocumentoExigidoIdFixo(3, variante), fase, TipoDocumentoOrigemIdFixo(3),
            "EXTRATO_BANCARIO", "Extrato bancário dos últimos três meses", "RENDA", obrigatorio: false);
        NoExigencia filhoDeclaracao = NoExigencia.ReidratarFolha(NoExigenciaIdFixo(2, variante), 0, declaracao, 1, null, null);
        NoExigencia filhoExtrato = NoExigencia.ReidratarFolha(NoExigenciaIdFixo(3, variante), 1, extrato, 1, null, null);
        return NoExigencia.ReidratarGrupo(NoExigenciaIdFixo(4, variante), 1, new CombinacaoDoGrupo(TipoNo.GrupoOu, 1, null),
            [], Ordem([filhoDeclaracao, filhoExtrato], permutar), null);
    }

    internal static Guid TipoDocumentoOrigemIdFixo(int indice) =>
        new($"77771111-0000-4000-8000-00000000000{indice:x}");

    internal static Guid DocumentoExigidoIdFixo(int ordem, int variante) =>
        new($"7777200{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static Guid NoExigenciaIdFixo(int ordem, int variante) =>
        new($"7777300{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    internal static DocumentoExigido DocumentoExigidoRico(
        Guid id, Guid exigidoNaFaseId, Guid tipoDocumentoOrigemId, string tipoDocumentoCodigo,
        string tipoDocumentoNome, string tipoDocumentoCategoria, bool obrigatorio) =>
        DocumentoExigido.Reidratar(
            id,
            exigidoNaFaseId,
            tipoDocumentoOrigemId: tipoDocumentoOrigemId,
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: null,
            condicoes: [],
            basesLegais: [],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!,
            tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);

    /// <summary>
    /// Id fixo de produto — como o das etapas e o das fases, ele entra no envelope, e sortear
    /// um Guid novo a cada montagem faria os bytes nunca baterem entre duas construções do
    /// mesmo corpus.
    /// </summary>
}

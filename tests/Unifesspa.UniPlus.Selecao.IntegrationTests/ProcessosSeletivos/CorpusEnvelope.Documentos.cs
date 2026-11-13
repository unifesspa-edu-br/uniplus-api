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

internal static partial class CorpusEnvelope
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
    private static IReadOnlyList<NoExigencia> ArvoreSatisfacaoRica(int variante, bool permutar)
    {
        Guid faseInscricaoId = FaseInscricao(variante).Id;

        DocumentoExigido historicoEscolar = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(1, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(1), "HISTORICO_ESCOLAR",
            "Histórico escolar do ensino médio", "ACADEMICO", obrigatorio: true);
        DocumentoExigido declaracaoImpostoRenda = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(2, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(2), "DECLARACAO_IMPOSTO_RENDA",
            "Declaração de Imposto de Renda", "RENDA", obrigatorio: false);
        DocumentoExigido extratoBancario = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(3, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(3), "EXTRATO_BANCARIO",
            "Extrato bancário dos últimos três meses", "RENDA", obrigatorio: false);

        NoExigencia raizSolteira = NoExigencia.Reidratar(
            NoExigenciaIdFixo(1, variante), TipoNo.Folha, ordem: 0,
            documentoExigidoId: historicoEscolar.Id, documentoExigido: historicoEscolar,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoDeclaracao = NoExigencia.Reidratar(
            NoExigenciaIdFixo(2, variante), TipoNo.Folha, ordem: 0,
            documentoExigidoId: declaracaoImpostoRenda.Id, documentoExigido: declaracaoImpostoRenda,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoExtrato = NoExigencia.Reidratar(
            NoExigenciaIdFixo(3, variante), TipoNo.Folha, ordem: 1,
            documentoExigidoId: extratoBancario.Id, documentoExigido: extratoBancario,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia grupoRenda = NoExigencia.Reidratar(
            NoExigenciaIdFixo(4, variante), TipoNo.GrupoOu, ordem: 1,
            documentoExigidoId: null, documentoExigido: null,
            quantidadeMinima: 1, consequencia: null, chaveDistincao: null, dataReferencia: null,
            ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [],
            filhos: Ordem([filhoDeclaracao, filhoExtrato], permutar));

        return Ordem([raizSolteira, grupoRenda], permutar);
    }

    private static Guid TipoDocumentoOrigemIdFixo(int indice) =>
        new($"77771111-0000-4000-8000-00000000000{indice:x}");

    private static Guid DocumentoExigidoIdFixo(int ordem, int variante) =>
        new($"7777200{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static Guid NoExigenciaIdFixo(int ordem, int variante) =>
        new($"7777300{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static DocumentoExigido DocumentoExigidoRico(
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

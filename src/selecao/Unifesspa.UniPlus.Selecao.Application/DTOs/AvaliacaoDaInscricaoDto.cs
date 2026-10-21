namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que o candidato consulta enquanto preenche, sem gravar nada (ADR-0143): as respostas como estão
/// na tela e as ocorrências dos grupos. O campo ou grupo ausente vale o que a inscrição já gravou.
/// <see cref="OfertaCursoId"/> é a oferta de curso que o candidato tem no seletor, ainda sem salvar: a
/// avaliação lê as modalidades e as regras dessa oferta. Nula ou ausente, nenhuma oferta está escolhida
/// — a mesma leitura do salvar, que grava a oferta nula —, nunca a gravada na inscrição; uma oferta que
/// o processo não conhece não é recusada aqui — o envio e o rascunho a recusam — e a avaliação trata
/// como se nenhuma tivesse sido escolhida. <see cref="OfertaCursoSegundaOpcaoId"/> é a 2ª opção, com a
/// mesma leitura: nula ou ausente, não há 2ª opção.
/// </summary>
public sealed record AvaliacaoDaInscricaoRequest(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? Grupos,
    Guid? OfertaCursoId = null,
    Guid? OfertaCursoSegundaOpcaoId = null);

/// <summary>
/// A avaliação da inscrição diante das respostas do candidato: as modalidades em que ele concorre e os
/// documentos que o edital exige dele, por fase. <see cref="Modalidades"/> são as da 1ª opção de curso e
/// <see cref="ModalidadesSegundaOpcao"/> as da 2ª, que tem a própria distribuição de vagas — nulas quando
/// a inscrição não tem 2ª opção. Os documentos valem para as duas opções.
/// </summary>
public sealed record AvaliacaoDaInscricaoDto(
    ModalidadesDaInscricaoDto Modalidades, DocumentosDaInscricaoDto Documentos, ModalidadesDaInscricaoDto? ModalidadesSegundaOpcao = null)
{
    /// <param name="fases">As fases que a resposta lista, na ordem do cronograma.</param>
    /// <param name="enderecoDoModelo">O endereço no acervo público do modelo que a exigência oferece.</param>
    public static AvaliacaoDaInscricaoDto De(
        ResultadoDaAvaliacaoDaInscricao resultado,
        IEnumerable<FaseCronograma> fases,
        Func<ModeloDaExigencia, Uri> enderecoDoModelo,
        IReadOnlyList<ArquivoDaApresentacaoDto> arquivos)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(enderecoDoModelo);
        ArgumentNullException.ThrowIfNull(arquivos);

        ILookup<Guid, DocumentoAvaliado> documentosPorFase = resultado.Documentos.Itens
            .Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido)
            .ToLookup(static d => d.Documento.ExigidoNaFaseId);
        return new AvaliacaoDaInscricaoDto(
            ProjetarModalidades(resultado.Modalidades),
            new DocumentosDaInscricaoDto(
                [.. fases.OrderBy(static f => f.Ordem).Select(f => new FaseDosDocumentosDto(
                    f.Id,
                    f.Codigo,
                    f.Ordem,
                    [.. documentosPorFase[f.Id].Select(d => Documento(d, enderecoDoModelo, arquivos))],
                    [.. resultado.Documentos.PendenciasPorFase.GetValueOrDefault(f.Id, []).Select(Pendencia)]))]),
            resultado.ModalidadesSegundaOpcao is { } daSegunda ? ProjetarModalidades(daSegunda) : null);
    }

    private static ModalidadesDaInscricaoDto ProjetarModalidades(ModalidadesDeConcorrencia modalidades) =>
        new(
            modalidades.Derivada,
            modalidades.Situacao.ToCodigo(),
            [.. modalidades.Itens.Select(static m => new ModalidadeDaInscricaoDto(m.Codigo, m.Nome, m.Ordem))],
            [.. modalidades.Pendencias.Select(Pendencia)]);

    private static PendenciaDaAvaliacaoDto Pendencia(PendenciaDaAvaliacao pendencia) =>
        new(pendencia.FatoCodigo, pendencia.Rotulo, pendencia.SecaoCodigo);

    private static DocumentoDaInscricaoDto Documento(DocumentoAvaliado avaliado, Func<ModeloDaExigencia, Uri> enderecoDoModelo, IReadOnlyList<ArquivoDaApresentacaoDto> arquivos)
    {
        DocumentoExigido documento = avaliado.Documento;
        return new DocumentoDaInscricaoDto(
            documento.Id,
            documento.TipoDocumentoCodigo,
            documento.TipoDocumentoNome,
            documento.Obrigatorio,
            documento.Finalidade is { } finalidade ? EstruturaFormulario.ParaToken(finalidade) : null,
            documento.ExigidoNaEtapaId,
            new FormatosAceitosCertameDto(
                documento.FormatosPermitidos.Qualquer,
                documento.FormatosPermitidos.Lista?.Select(static e => e.Formato.ToCodigo()).ToList()),
            documento.Modelo is { } modelo
                ? new ModeloDocumentalCertameDto(modelo.NomeArquivo, ModeloDeDocumento.TokenDe(modelo.Formato), modelo.HashSha256, enderecoDoModelo(modelo))
                : null,
            avaliado.Situacao.ToCodigo(),
            avaliado.GrupoCodigo,
            avaliado.OcorrenciaId,
            [.. avaliado.Grupos.Select(static g => new GrupoDaArvoreDto(g.GrupoId, g.Tipo.ToCodigo(), g.Minimo, g.RepetePorEntidade))],
            avaliado.RepetePorEntidade,
            [.. ArquivoDaApresentacao.FormatosAceitosPor(documento).Select(static f => f.ToCodigo())],
            ArquivoDaApresentacao.LimiteAnunciadoDe(documento),
            avaliado.QuantidadeMinima,
            [.. arquivos.Where(a => a.ExigenciaId == documento.Id && a.OcorrenciaId == avaliado.OcorrenciaId)]);
    }
}

/// <summary>
/// Os documentos que o edital exige do candidato, diante das respostas dele (UNI-REQ-0069, UNI-REQ-0077),
/// pelas fases do cronograma.
/// </summary>
public sealed record DocumentosDaInscricaoDto(IReadOnlyList<FaseDosDocumentosDto> Fases);

/// <summary>
/// Uma fase do cronograma, as exigências dela que se aplicam ou podem se aplicar ao candidato, e o que
/// falta responder para saber se as que podem ser exigidas nela se aplicam.
/// </summary>
public sealed record FaseDosDocumentosDto(
    Guid FaseId,
    string Codigo,
    int Ordem,
    IReadOnlyList<DocumentoDaInscricaoDto> Documentos,
    IReadOnlyList<PendenciaDaAvaliacaoDto> Pendencias);

/// <summary>
/// Uma exigência documental diante do candidato.
/// </summary>
/// <param name="ExigenciaId">A exigência na versão fixada, que identifica a vaga do documento.</param>
/// <param name="Finalidade">O formulário em que o documento é apresentado, como <c>INSCRICAO</c> ou <c>ISENCAO_TAXA</c>; nulo quando a exigência não o declara.</param>
/// <param name="EtapaId">A etapa da fase em que o documento é exigido; nula quando vale a fase inteira.</param>
/// <param name="Situacao">
/// <c>PENDENTE</c> — se aplica e falta enviar —, <c>SATISFEITA</c> — se aplica e os documentos enviados a atendem —,
/// <c>PODE_SER_EXIGIDO</c> — uma resposta que ainda falta decide — ou
/// <c>DEPENDE_DE_FASE_POSTERIOR</c> — decide um fato que só se conhece depois da inscrição.
/// </param>
/// <param name="GrupoCodigo">O grupo de membros a que a exigência se liga, uma vez por membro; nulo fora de repetição.</param>
/// <param name="OcorrenciaId">A ocorrência do grupo a que a exigência se liga; nula quando ainda não se sabe quais membros o grupo tem.</param>
/// <param name="FormatosAceitos">Os formatos que a exigência aceita (PDF, JPEG, PNG); "qualquer" são os três.</param>
/// <param name="TamanhoMaximoBytes">O menor limite efetivo entre os formatos aceitos; o servidor confere o do formato escolhido.</param>
/// <param name="QuantidadeMinima">Quantas apresentações a exigência pede; uma apresentação com frente e verso conta uma.</param>
/// <param name="Arquivos">Os arquivos confirmados desta exigência e ocorrência.</param>
/// <param name="Grupos">
/// Os grupos da árvore de exigências que contêm a exigência, do mais externo ao mais interno. Os
/// documentos que dividem um grupo dividem o <c>grupoId</c>, e assim se reconstrói a escolha: o OU, o
/// N-de e o E dentro deles. A repetição por membro vale do grupo que a declara para baixo.
/// </param>
/// <param name="RepetePorEntidade">
/// O grupo de membros pelo qual a própria exigência se repete, quando a repetição começa nela; nulo
/// quando ela não se repete ou quando a repetição vem de um grupo que a contém.
/// </param>
public sealed record DocumentoDaInscricaoDto(
    Guid ExigenciaId,
    string TipoDocumentoCodigo,
    string Nome,
    bool Obrigatorio,
    string? Finalidade,
    Guid? EtapaId,
    FormatosAceitosCertameDto Formatos,
    ModeloDocumentalCertameDto? Modelo,
    string Situacao,
    string? GrupoCodigo,
    string? OcorrenciaId,
    IReadOnlyList<GrupoDaArvoreDto> Grupos,
    string? RepetePorEntidade,
    IReadOnlyList<string> FormatosAceitos,
    long TamanhoMaximoBytes,
    int QuantidadeMinima,
    IReadOnlyList<ArquivoDaApresentacaoDto> Arquivos);

/// <summary>
/// Um grupo da árvore de exigências: o nó, o tipo — <c>E</c>, em que todos os filhos são exigidos, ou
/// <c>OU</c>, em que bastam <see cref="Minimo"/> deles, o N-de quando o mínimo passa de um —, o mínimo,
/// nulo no E, e o grupo de membros pelo qual o nó se repete, quando a repetição começa nele: a escolha
/// vale então para cada membro.
/// </summary>
public sealed record GrupoDaArvoreDto(Guid GrupoId, string Tipo, int? Minimo, string? RepetePorEntidade);

/// <summary>
/// As modalidades em que o candidato concorre (UNI-REQ-0076). Sem derivação da modalidade na versão,
/// <see cref="Derivada"/> é falso e <see cref="Situacao"/> é nula. A situação é <c>CONCORRE</c>,
/// <c>NAO_CONCORRE</c> ou <c>FALTA_RESPONDER</c>; nesta, <see cref="Pendencias"/> diz o que falta.
/// </summary>
public sealed record ModalidadesDaInscricaoDto(
    bool Derivada,
    string? Situacao,
    IReadOnlyList<ModalidadeDaInscricaoDto> Itens,
    IReadOnlyList<PendenciaDaAvaliacaoDto> Pendencias);

/// <summary>Uma modalidade em que o candidato concorre, com o nome congelado na publicação e a ordem das ofertadas.</summary>
public sealed record ModalidadeDaInscricaoDto(string Codigo, string Nome, int Ordem);

/// <summary>Um campo ou grupo que falta responder, com o rótulo e a seção do formulário em que está.</summary>
public sealed record PendenciaDaAvaliacaoDto(string FatoCodigo, string Rotulo, string? SecaoCodigo);

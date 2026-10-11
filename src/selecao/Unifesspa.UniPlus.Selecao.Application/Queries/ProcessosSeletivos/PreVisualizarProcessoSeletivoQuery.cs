namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia a configuração viva do processo — rascunho ou sessão de retificação, nunca o envelope
/// publicado — contra um perfil simulado de candidato: o que cada formulário mostra e exige e os
/// documentos que a árvore de exigências pediria (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0064).
/// </summary>
/// <remarks>
/// O perfil carrega fatos pessoais e sensíveis simulados: não é gravado nem registrado em log.
/// </remarks>
public sealed record PreVisualizarProcessoSeletivoQuery(Guid ProcessoSeletivoId, PreVisualizacaoDoProcessoInput Simulacao)
    : IQuery<Result<PreVisualizacaoDoProcessoDto>>;

/// <summary>
/// A pré-visualização usa a mesma avaliação do candidato (<see cref="AvaliacaoDaInscricao"/>): o
/// formulário, com as derivações e os agregados do processo intercalados, e a árvore de exigências,
/// sem apresentação nenhuma — a folha aplicável sai pendente, isto é, exigida.
/// </summary>
public static class PreVisualizarProcessoSeletivoQueryHandler
{
    public const string OcorrenciaInvalida = "ProcessoSeletivo.OcorrenciaSimuladaInvalida";

    public static async Task<Result<PreVisualizacaoDoProcessoDto>> Handle(
        PreVisualizarProcessoSeletivoQuery query,
        IProcessoSeletivoRepository repository,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        ProcessoSeletivo? processo = await repository.ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        PreVisualizacaoDoProcessoInput simulacao = query.Simulacao ?? new(null, null, null, null);
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) = OcorrenciasRecebidas.Ler(
            simulacao.Grupos?.Select(static g => KeyValuePair.Create(
                g.Key, (IReadOnlyList<OcorrenciaRecebida>?)g.Value?.Select(static o => new OcorrenciaRecebida(o?.Id, o?.Respostas)).ToList())),
            OcorrenciaInvalida);

        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        Result<DefinicaoAvaliavel> montada = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, fatosDoCatalogo);
        if (montada.IsFailure)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(montada.Error!);
        }

        if (erros.Count > 0)
        {
            return Result<PreVisualizacaoDoProcessoDto>.ValidationFailure(erros);
        }

        Result<ResultadoDaAvaliacaoDaInscricao> avaliada = AvaliacaoDaInscricao.Avaliar(
            ConfiguracaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, montada.Value!),
            new EntradaDaAvaliacaoDaInscricao(
                simulacao.Respostas ?? new Dictionary<string, JsonElement>(),
                ocorrencias,
                RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos),
                new HashSet<string>(
                    (simulacao.EtapasConcluidas ?? [])
                        .Where(static e => e is not null)
                        .Select(static e => DefinicaoDoProcesso.CodigoDaEtapa(EstruturaFormulario.FinalidadeDoToken(e.Finalidade), e.Etapa)),
                    StringComparer.Ordinal),
                Envio: null));
        if (avaliada.IsFailure)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(avaliada.Error!);
        }

        return Result<PreVisualizacaoDoProcessoDto>.Success(new PreVisualizacaoDoProcessoDto(
            Formularios(processo, avaliada.Value!.Formulario),
            [.. avaliada.Value!.Documentos.Select(Documento)]));
    }

    private static List<FormularioSimuladoDto> Formularios(ProcessoSeletivo processo, AvaliacaoFormulario avaliacao)
    {
        Dictionary<string, FatoColetado> itemPorFato = processo.FatosColetados.ToDictionary(static f => f.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, GrupoColetado> grupoPorCodigo = processo.GruposColetados.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, TermoExigidoFormulario> termoPorCodigo = processo.TermosExigidos
            .ToDictionary(static t => DefinicaoDoProcesso.CodigoDoTermo(t.Finalidade, t.Codigo), StringComparer.Ordinal);

        return [.. processo.Formularios.OrderBy(static f => f.Finalidade).Select(formulario => new FormularioSimuladoDto(
            EstruturaFormulario.ParaToken(formulario.Finalidade),
            [.. avaliacao.Itens
                .Where(i => itemPorFato[i.FatoCodigo].Finalidade == formulario.Finalidade)
                .Select(i => Item(i, itemPorFato[i.FatoCodigo]))],
            [.. avaliacao.Grupos
                .Where(g => grupoPorCodigo[g.Codigo].Finalidade == formulario.Finalidade)
                .Select(g => new GrupoSimuladoDto(
                    g.Codigo, grupoPorCodigo[g.Codigo].EtapaCodigo, g.Visivel.ToCodigo(), g.Obrigatorio.ToCodigo(),
                    g.ContagemValida, g.OcorrenciaDoCandidatoValida,
                    [.. g.Ocorrencias.Select(o => new OcorrenciaSimuladaDto(o.Id, [.. o.Itens.Select(i => Item(
                        i, grupoPorCodigo[g.Codigo].Subitens.Single(s => s.FatoCodigo == i.FatoCodigo), grupoPorCodigo[g.Codigo].EtapaCodigo))]))]))],
            [.. avaliacao.Termos
                .Where(t => termoPorCodigo[t.Codigo].Finalidade == formulario.Finalidade)
                .Select(t => new TermoSimuladoDto(termoPorCodigo[t.Codigo].Codigo, t.Visivel.ToCodigo(), t.Obrigatorio.ToCodigo()))],
            [.. formulario.Etapas
                .Where(static e => e.Tipo == TipoEtapaFormulario.Secao)
                .OrderBy(static e => e.Ordem)
                .Select(e => new SecaoSimuladaDto(
                    e.Codigo,
                    avaliacao.Etapas.Single(a => a.Codigo == DefinicaoDoProcesso.CodigoDaEtapa(formulario.Finalidade, e.Codigo)).Visivel.ToCodigo()))]))];
    }

    /// <summary>O item avaliado; o campo de grupo segue a seção do grupo.</summary>
    private static ItemSimuladoDto Item(AvaliacaoItem item, FatoColetado campo, string? etapaDoGrupo = null) => new(
        item.FatoCodigo, etapaDoGrupo ?? campo.EtapaCodigo, item.Visivel.ToCodigo(), item.Obrigatorio.ToCodigo(),
        [.. item.RestricoesVioladas.Select(static r => RestricaoValorJson.ParaToken(r.Tipo))],
        item.Impedido.ToCodigo(), campo.Impedimento?.Mensagem,
        item.Opcoes is { } opcoes ? new OpcoesSimuladasDto(opcoes.Codigos, opcoes.Definitivas) : null);

    private static DocumentoSimuladoDto Documento(DocumentoAvaliado avaliado)
    {
        DocumentoExigido documento = avaliado.Documento;
        return new(
            documento.Id, documento.TipoDocumentoCodigo, documento.TipoDocumentoNome, documento.Obrigatorio,
            documento.ExigidoNaFaseId, documento.Finalidade is { } finalidade ? EstruturaFormulario.ParaToken(finalidade) : null,
            documento.ExigidoNaEtapaId, Situacao(avaliado.Status), avaliado.OcorrenciaId,
            [.. avaliado.Alternativas.Select(static a => new AlternativasSimuladasDto(a.GrupoId, a.Minimo))]);
    }

    private static string Situacao(StatusResolucaoExigencia status) => status switch
    {
        StatusResolucaoExigencia.Pendente or StatusResolucaoExigencia.Satisfeita => "EXIGIDO",
        StatusResolucaoExigencia.NaoAplicavel => "NAO_EXIGIDO",
        _ => "INDETERMINADO",
    };
}

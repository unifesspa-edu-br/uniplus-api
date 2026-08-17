namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirCascataRemanejamentoCommand"/>
/// (RN-CASCATA-1..5, Story #575): campos todos nulos remove a cascata
/// (<see cref="ProcessoSeletivo.DefinirCascataRemanejamento"/> com
/// <see langword="null"/>); todos presentes resolve a regra
/// <c>criterio_remanejamento</c> no catálogo, monta a cascata pela factory
/// (forma — RN-CASCATA-4) e só então compara célula a célula contra o
/// <c>esquema_args</c> congelado da regra (conteúdo — RN-CASCATA-5), nessa
/// ordem — ver §4 da story para o porquê da ordem.
/// </summary>
public static class DefinirCascataRemanejamentoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCascataRemanejamentoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        bool tudoNulo = command.RegraCodigo is null && command.RegraVersao is null
            && command.FallbackCodigo is null && command.Destinos is null;
        bool tudoPresente = command.RegraCodigo is not null && command.RegraVersao is not null
            && command.FallbackCodigo is not null && command.Destinos is not null;

        if (tudoNulo)
        {
            Result removerResult = processo.DefinirCascataRemanejamento(null, command.Precondicao);
            if (removerResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(removerResult.Error!);
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
        }

        if (!tudoPresente)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.CamposObrigatorios",
                "RegraCodigo, RegraVersao, FallbackCodigo e Destinos precisam estar todos presentes " +
                "(definição) ou todos ausentes (remoção)."));
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(command.RegraCodigo!, command.RegraVersao!, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.RegraNaoEncontrada",
                $"Regra de remanejamento {command.RegraCodigo}/{command.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.CriterioRemanejamento)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.RegraTipoInvalido",
                $"A regra {command.RegraCodigo}/{command.RegraVersao} não é do tipo criterio_remanejamento."));
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(referenciaRegraResult.Error!);
        }

        // Acumula (ADR-0125) as violações de TODOS os itens em vez de parar no primeiro —
        // um payload com vários destinos malformados reporta todos de uma vez, cada um com
        // o índice do item na lista prefixado ao field (ex.: "destinos[2].ordem"). Se algum
        // item falhar, a checagem de coerência entre itens (ConfiguracaoCascataRemanejamento.
        // Criar) não roda: ela pressupõe destinos já válidos individualmente.
        List<DestinoRemanejamento> destinos = [];
        List<FieldError> itemErros = [];
        for (int indice = 0; indice < command.Destinos!.Count; indice++)
        {
            DestinoRemanejamentoInput? destinoInput = command.Destinos[indice];

            // Defesa em profundidade: o FluentValidation (middleware, antes deste handler) já
            // recusa um item nulo, mas o handler não deve confiar exclusivamente nisso para
            // não desreferenciar null — um payload que contornasse a validação de borda não
            // pode virar 500.
            if (destinoInput is null)
            {
                itemErros.Add(new($"destinos[{indice}]", new DomainError(
                    "ConfiguracaoCascataRemanejamento.CamposObrigatorios",
                    "Nenhum item da lista de destinos pode ser nulo.")));
                continue;
            }

            Result<DestinoRemanejamento> destinoResult = DestinoRemanejamento.Criar(
                destinoInput.ModalidadeOrigemCodigo, destinoInput.Ordem, destinoInput.ModalidadeDestinoCodigo);
            if (destinoResult.IsFailure)
            {
                itemErros.AddRange(destinoResult.Errors.Select(erro => erro with { Field = $"destinos[{indice}].{erro.Field}" }));
                continue;
            }

            destinos.Add(destinoResult.Value!);
        }

        if (itemErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(itemErros);
        }

        Result<ConfiguracaoCascataRemanejamento> cascataResult = ConfiguracaoCascataRemanejamento.Criar(
            referenciaRegraResult.Value!, command.FallbackCodigo!, destinos);
        if (cascataResult.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(cascataResult.Errors);
        }

        ConfiguracaoCascataRemanejamento cascata = cascataResult.Value!;
        if (!BateComEsquemaArgs(cascata, regra.EsquemaArgs))
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra",
                $"O fallback e/ou os destinos informados divergem do esquema_args congelado da regra {command.RegraCodigo}/{command.RegraVersao}."));
        }

        Result definirResult = processo.DefinirCascataRemanejamento(cascata, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    /// <summary>
    /// RN-CASCATA-5: o payload aplicado (fallback + todos os destinos, célula a
    /// célula) precisa ser idêntico ao <c>esquema_args</c> congelado da versão da
    /// regra — não só a forma (já garantida pela factory). Shape esperado:
    /// <c>{"fallbackCodigo": "AC", "ordens": [{"origem": "...", "destinos": ["...", ...]}]}</c>.
    /// </summary>
    private static bool BateComEsquemaArgs(ConfiguracaoCascataRemanejamento cascata, JsonElement esquemaArgs)
    {
        if (!esquemaArgs.TryGetProperty("fallbackCodigo", out JsonElement fallbackElemento)
            || !string.Equals(fallbackElemento.GetString(), cascata.FallbackCodigo, StringComparison.Ordinal))
        {
            return false;
        }

        if (!esquemaArgs.TryGetProperty("ordens", out JsonElement ordensElemento) || ordensElemento.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        Dictionary<string, List<string>> destinosPorOrigemNaCascata = cascata.Destinos
            .GroupBy(static d => d.ModalidadeOrigemCodigo, StringComparer.Ordinal)
            .ToDictionary(
                static grupo => grupo.Key,
                static grupo => grupo.OrderBy(static d => d.Ordem).Select(static d => d.ModalidadeDestinoCodigo).ToList(),
                StringComparer.Ordinal);

        // Conjunto de origens já conferidas — não uma contagem. Um esquema_args corrompido
        // que repita uma origem não pode "cobrir" outra origem por coincidência de contagem
        // (achado de revisão): a origem repetida é rejeitada explicitamente por Add devolver
        // false na segunda ocorrência, e a checagem final compara o CONJUNTO de origens
        // conferidas contra o conjunto exigido pela cascata, não o tamanho de cada um.
        HashSet<string> origensConferidas = new(StringComparer.Ordinal);
        foreach (JsonElement itemOrigem in ordensElemento.EnumerateArray())
        {
            if (!itemOrigem.TryGetProperty("origem", out JsonElement origemElemento)
                || !itemOrigem.TryGetProperty("destinos", out JsonElement destinosElemento)
                || destinosElemento.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            string? origem = origemElemento.GetString();
            if (origem is null || !origensConferidas.Add(origem)
                || !destinosPorOrigemNaCascata.TryGetValue(origem, out List<string>? destinosDaCascata))
            {
                return false;
            }

            List<string?> destinosDoEsquema = [.. destinosElemento.EnumerateArray().Select(static d => d.GetString())];
            if (destinosDoEsquema.Count != destinosDaCascata.Count
                || !destinosDoEsquema.SequenceEqual(destinosDaCascata, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return origensConferidas.SetEquals(destinosPorOrigemNaCascata.Keys);
    }
}
